﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mizekar.Core.Data;
using Mizekar.Core.Data.Services;
using Mizekar.Core.Model.Api;
using Mizekar.Core.Model.Api.Response;
using Mizekar.Micro.Idea.Data;
using Mizekar.Micro.Idea.Data.Entities;
using Mizekar.Micro.Idea.Models;
using Mizekar.Micro.Idea.Models.IdeaAssessmentOptions;
using NSwag.Annotations;

namespace Mizekar.Micro.Idea.Controllers
{
    /// <summary>
    /// Idea Assessment Scores Management - مدیریت امتیازهای ارزیابی
    /// </summary>
    [Route("api/v1/[controller]")]
    [ApiController]
    [SwaggerTag(name: "IdeaAssessmentScores", Name = "IdeaAssessmentScores", Description = "Idea Assessment Scores Management - مدیریت امتیازهای ارزیابی")]
    public class IdeaAssessmentScoresController : ControllerBase
    {
        private readonly DbSet<IdeaAssessmentScore> _ideaAssessmentScores;
        private readonly IdeaDbContext _context;
        private readonly IMapper _mapper;
        private readonly IUserResolverService _userResolverService;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <param name="mapper"></param>
        /// <param name="userResolverService"></param>
        public IdeaAssessmentScoresController(IdeaDbContext context, IMapper mapper, IUserResolverService userResolverService)
        {
            _context = context;
            _mapper = mapper;
            _userResolverService = userResolverService;
            _ideaAssessmentScores = _context.IdeaAssessmentScores;
        }

        private async Task<Paged<IdeaAssessmentScoreViewPoco>> ToPaged(IQueryable<IdeaAssessmentScore> source, int pageNumber, int pageSize)
        {
            var totalCount = source.Count();
            var entities = await source.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

            var models = new List<IdeaAssessmentScoreViewPoco>();
            foreach (var ideaAssessmentScore in entities)
            {
                models.Add(ConvertToModel(ideaAssessmentScore));
            }

            var resultPaged = new Paged<IdeaAssessmentScoreViewPoco>()
            {
                Items = models,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                PagesCount = 0
            };
            return resultPaged;
        }

        private IdeaAssessmentScoreViewPoco ConvertToModel(IdeaAssessmentScore ideaAssessmentScore)
        {
            return new IdeaAssessmentScoreViewPoco()
            {
                Id = ideaAssessmentScore.Id,
                IdeaAssessmentScore = _mapper.Map<IdeaAssessmentScorePoco>(ideaAssessmentScore),
                Idea = _mapper.Map<IdeaPoco>(ideaAssessmentScore.Idea),
                BusinessBaseInfo = _mapper.Map<BusinessBaseInfo>(ideaAssessmentScore)
            };
        }

        /// <summary>
        /// Get Last Idea Assessment Scores
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [ProducesResponseType(typeof(Paged<IdeaAssessmentScoreViewPoco>), 200)]
        public async Task<ActionResult<Paged<IdeaAssessmentScoreViewPoco>>> GetLastIdeaAssessmentScores(int pageNumber, int pageSize)
        {
            var query = _ideaAssessmentScores
                .Include(i => i.Idea)
                .OrderByDescending(o => o.CreatedOn)
                .AsQueryable();
            var resultPaged = await ToPaged(query, pageNumber, pageSize);
            return Ok(resultPaged);
        }

        /// <summary>
        /// Get Last Idea Assessment Scores By userId
        /// </summary>
        /// <returns></returns>
        [HttpGet("user/{userId}")]
        [ProducesResponseType(typeof(Paged<IdeaAssessmentScoreViewPoco>), 200)]
        public async Task<ActionResult<Paged<IdeaAssessmentScoreViewPoco>>> GetLastIdeaAssessmentScoresByUserId([FromRoute] long userId, int pageNumber, int pageSize)
        {
            var query = _ideaAssessmentScores
                .Where(w => w.CreatedById == userId)
                .Include(i => i.Idea)
                .OrderByDescending(o => o.CreatedOn)
                .AsQueryable();
            var resultPaged = await ToPaged(query, pageNumber, pageSize);
            return Ok(resultPaged);
        }

        /// <summary>
        /// Get Idea Assessment Selections By userId and ideaId
        /// </summary>
        /// <returns></returns>
        [HttpGet("user/{userId}/idea/{ideaId}")]
        [ProducesResponseType(typeof(List<Guid>), 200)]
        public async Task<ActionResult<List<Guid>>> GetIdeaAssessmentSelectionsByUserAndIdea([FromRoute] long userId, [FromRoute] Guid ideaId)
        {
            var query = _context.IdeaAssessmentOptionSelections
                .Where(w => w.CreatedById == userId)
                .Where(w => w.IdeaId == ideaId)
                //.Include(i => i.Idea)
                //.OrderByDescending(o => o.CreatedOn)
                .AsQueryable();
            var resultPaged = await query.Select(s => s.IdeaAssessmentOptionSetItemId).ToListAsync();
            return Ok(resultPaged);
        }

        /// <summary>
        /// Update Idea Assessment Selections By userId and ideaId
        /// </summary>
        /// <returns></returns>
        [HttpPut("idea/{ideaId}")]
        [ProducesResponseType(typeof(long), 200)]
        public async Task<ActionResult<long>> PutIdeaAssessmentSelectionsByUserAndIdea([FromRoute] Guid ideaId, List<Guid> ideaAssessmentOptionSelected)
        {
            if (ideaAssessmentOptionSelected == null)
            {
                return BadRequest("null Assessment items");
            }

            var userId = _userResolverService.UserId;
            var currentAssessmentoptionSelections = await _context.IdeaAssessmentOptionSelections
                .Where(w => w.CreatedById == userId)
                .Where(w => w.IdeaId == ideaId)
                //.Include(i => i.Idea)
                //.OrderByDescending(o => o.CreatedOn)
                .ToListAsync();

            // remove none selected
            foreach (var optionSelection in currentAssessmentoptionSelections)
            {
                if (!ideaAssessmentOptionSelected.Contains(optionSelection.IdeaAssessmentOptionSetItemId))
                {
                    optionSelection.IsDeleted = true;
                }
            }

            // add new relation
            foreach (var optionItemIdId in ideaAssessmentOptionSelected)
            {
                if (currentAssessmentoptionSelections.FirstOrDefault(f => f.IdeaAssessmentOptionSetItemId == optionItemIdId) == null)
                {
                    var optionSetItem = _context.IdeaOptionSetItems.FirstOrDefault(q => q.Id == optionItemIdId);
                    var newOptionSelection = new IdeaOptionSelection()
                    {
                        IdeaId = ideaId,
                        IdeaOptionSetId = optionSetItem.IdeaOptionSetId,
                        IdeaOptionSetItemId = optionItemIdId
                    };
                    _context.Add(newOptionSelection);
                }
            }
            await _context.SaveChangesAsync();

            var sumScore = await _context.IdeaAssessmentOptionSelections
                .Where(w => w.CreatedById == userId)
                .Where(w => w.IdeaId == ideaId)
                .SumAsync(a => a.IdeaAssessmentOptionSetItem.Value);

            // update or create Assessment score
            var assessmentScoreRecord = await _ideaAssessmentScores
                .Where(w => w.CreatedById == userId)
                .Where(w => w.IdeaId == ideaId)
                .FirstOrDefaultAsync();
            if (assessmentScoreRecord == null)
            {
                assessmentScoreRecord = new IdeaAssessmentScore() { IdeaId = ideaId };
                _ideaAssessmentScores.Add(assessmentScoreRecord);
            }
            assessmentScoreRecord.Score = sumScore;
            await _context.SaveChangesAsync();

            return Ok(sumScore);
        }


        /// <summary>
        /// Get Last Idea Assessment Scores By IdeaId
        /// </summary>
        /// <returns></returns>
        [HttpGet("idea/{ideaId}")]
        [ProducesResponseType(typeof(Paged<IdeaAssessmentScoreViewPoco>), 200)]
        public async Task<ActionResult<Paged<IdeaAssessmentScoreViewPoco>>> GetLastIdeaAssessmentScoresByIdeaId([FromRoute] Guid ideaId, int pageNumber, int pageSize)
        {
            var query = _ideaAssessmentScores
                .Where(w => w.IdeaId == ideaId)
                .Include(i => i.Idea)
                .OrderByDescending(o => o.CreatedOn)
                .AsQueryable();
            var resultPaged = await ToPaged(query, pageNumber, pageSize);
            return Ok(resultPaged);
        }

        /// <summary>
        /// Get Idea Assessment Score By Id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(IdeaAssessmentScoreViewPoco), 200)]
        [ProducesResponseType(typeof(Guid), 404)]
        public async Task<ActionResult<IdeaAssessmentScoreViewPoco>> GetIdeaAssessmentScore([FromRoute] Guid id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var ideaAssessmentScore = await _ideaAssessmentScores.Include(i => i.Idea).FirstOrDefaultAsync(i => i.Id == id);

            if (ideaAssessmentScore == null)
            {
                return NotFound(id);
            }

            var poco = ConvertToModel(ideaAssessmentScore);
            return Ok(poco);
        }

        /// <summary>
        /// Update IdeaAssessmentScore
        /// </summary>
        /// <param name="id"></param>
        /// <param name="ideaAssessmentScorePoco"></param>
        /// <returns></returns>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(Guid), 200)]
        [ProducesResponseType(typeof(BadRequestObjectResult), 400)]
        [ProducesResponseType(typeof(Guid), 404)]
        public async Task<ActionResult<Guid>> PutIdeaAssessmentScore([FromRoute] Guid id, [FromBody] IdeaAssessmentScorePoco ideaAssessmentScorePoco)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (id == Guid.Empty)
            {
                return BadRequest();
            }

            var ideaAssessmentScoreEntity = await _ideaAssessmentScores.FirstOrDefaultAsync(q => q.Id == id);
            if (ideaAssessmentScoreEntity == null)
            {
                return NotFound(id);
            }

            _mapper.Map(ideaAssessmentScorePoco, ideaAssessmentScoreEntity);

            await _context.SaveChangesAsync();

            return Ok(id);
        }

        /// <summary>
        /// Create IdeaAssessmentScore
        /// </summary>
        /// <param name="ideaAssessmentScorePoco"></param>
        /// <returns></returns>
        [HttpPost]
        [ProducesResponseType(typeof(Guid), 200)]
        [ProducesResponseType(typeof(BadRequestObjectResult), 400)]
        public async Task<ActionResult<Guid>> PostIdeaAssessmentScore([FromBody] IdeaAssessmentScorePoco ideaAssessmentScorePoco)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var ideaAssessmentScoreEntity = _mapper.Map<IdeaAssessmentScore>(ideaAssessmentScorePoco);
            _ideaAssessmentScores.Add(ideaAssessmentScoreEntity);
            await _context.SaveChangesAsync();

            return Ok(ideaAssessmentScoreEntity.Id);
        }

        /// <summary>
        /// Delete IdeaAssessmentScore
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("{id}")]
        [ProducesResponseType(typeof(Guid), 200)]
        [ProducesResponseType(typeof(BadRequestObjectResult), 400)]
        [ProducesResponseType(typeof(Guid), 404)]
        public async Task<ActionResult<Guid>> DeleteIdeaAssessmentScore([FromRoute] Guid id)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var ideaAssessmentScore = await _ideaAssessmentScores.FirstOrDefaultAsync(q => q.Id == id);
            if (ideaAssessmentScore == null)
            {
                return NotFound(id);
            }
            MarkAsDelete(ideaAssessmentScore);
            await _context.SaveChangesAsync();

            return Ok(id);
        }

        private void MarkAsDelete(IBusinessBaseEntity businessBaseEntity)
        {
            businessBaseEntity.IsDeleted = true;
        }
    }
}