using System.Linq.Expressions;
using backend.Entities;
namespace backend.DTOs.Tickets;

internal static class TicketQueries
{
    public static Expression<Func<Ticket, TicketDto>> ProjectToDto()
    {
        return ticket => new TicketDto
        {
            Id = ticket.Id,
            Reference = ticket.Reference,
            Subject = ticket.Subject,
            Description = ticket.Description,
            Status = ticket.Status,
            Priority = ticket.Priority,
            CategoryId = ticket.CategoryId,
            CategoryName = ticket.Category.Name,
            RequesterId = ticket.RequesterId,
            RequesterName = ticket.Requester.Name,
            AssigneeId = ticket.AssigneeId,
            AssigneeName = ticket.Assignee != null ? ticket.Assignee.Name : null,
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt
        };
    }

    public static Expression<Func<Ticket, TicketDetailDto>> ProjectToDetailDto()
    {
        return ticket => new TicketDetailDto
        {
            Id = ticket.Id,
            Reference = ticket.Reference,
            Subject = ticket.Subject,
            Description = ticket.Description,
            Status = ticket.Status,
            Priority = ticket.Priority,
            CategoryId = ticket.CategoryId,
            CategoryName = ticket.Category.Name,
            RequesterId = ticket.RequesterId,
            RequesterName = ticket.Requester.Name,
            AssigneeId = ticket.AssigneeId,
            AssigneeName = ticket.Assignee != null ? ticket.Assignee.Name : null,
            CreatedAt = ticket.CreatedAt,
            UpdatedAt = ticket.UpdatedAt,
            Comments = ticket.Comments
                .OrderBy(c => c.CreatedAt)
                .Select(c => new TicketCommentDto
                {
                    Id = c.Id,
                    Body = c.Body,
                    AuthorId = c.AuthorId,
                    AuthorName = c.Author.Name,
                    CreatedAt = c.CreatedAt
                })
                .ToList()
        };
    }
}