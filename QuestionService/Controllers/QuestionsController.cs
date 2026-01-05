using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestionService.Data;
using QuestionService.DTOs;
using QuestionService.Models;

namespace QuestionService.Controllers;

[Route("[controller]")]
[ApiController]
public class QuestionsController(QuestionDbContext db) : ControllerBase
{
    [Authorize]
    [HttpPost]
    public async Task<ActionResult<Question>> CreateQuestion(CreateQuestionDto dto)
    {

        var validTags = db.Tags
            .Where(t => dto.Tags.Contains(t.Slug))
            .ToList();
        var missingTags = dto.Tags.Except(validTags.Select(t => t.Slug)).ToList();

        if (missingTags.Count > 0)
        {
            return BadRequest($"The following tags do not exist: {string.Join(", ", missingTags)}");
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var name = User.FindFirstValue("name");

        if (userId is null || name is null)
        {
            return BadRequest("Cannot get user details");
        }

        var question = new Question
        {
            Title = dto.Title,
            Content = dto.Content,
            AskerId = userId,
            AskerDisplayName = name,
            TagSlugs = dto.Tags
        };

        db.Questions.Add(question);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetQuestionById), new { id = question.Id }, question);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Question>> GetQuestionById(string id)
    {
        var question = await db.Questions.FindAsync(id);
        if (question is null)
        {
            return NotFound();
        }

        await db.Questions
            .Where(q => q.Id == id)
            .ExecuteUpdateAsync(q => q.SetProperty(q => q.ViewCount, q => q.ViewCount + 1));

        return question;
    }

    [HttpGet]
    public async Task<ActionResult<List<Question>>> GetQuestions(string? tag)
    {
        var query = db.Questions.AsQueryable();

        if (!string.IsNullOrEmpty(tag))
        {
            query = query.Where(q => q.TagSlugs.Contains(tag));
        }

        return await query.OrderByDescending(q => q.CreatedAt).ToListAsync();
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<ActionResult<Question>> UpdateQuestion(string id, CreateQuestionDto dto)
    {
        var question = await db.Questions.FindAsync(id);
        if (question is null)
        {
            return NotFound();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId != question.AskerId)
        {
            return Forbid();
        }

        var validTags = db.Tags
            .Where(t => dto.Tags.Contains(t.Slug))
            .ToList();
        var missingTags = dto.Tags.Except(validTags.Select(t => t.Slug)).ToList();

        if (missingTags.Count > 0)
        {
            return BadRequest($"The following tags do not exist: {string.Join(", ", missingTags)}");
        }

        question.Title = dto.Title;
        question.Content = dto.Content;
        question.TagSlugs = dto.Tags;
        question.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return NoContent();
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteQuestion(string id)
    {
        var question = await db.Questions.FindAsync(id);
        if (question is null)
        {
            return NotFound();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId != question.AskerId)
        {
            return Forbid();
        }

        db.Questions.Remove(question);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
