using FluentValidation;

namespace backend.DTOs.Tickets;

public sealed class UpdateTicketDtoValidator : AbstractValidator<UpdateTicketDto>
{
    public UpdateTicketDtoValidator()
    {
        // .When(...) so we only validate a field when the caller actually sent it
        RuleFor(x => x.Status!.Value)
            .IsInEnum()
            .When(x => x.Status.HasValue);

        RuleFor(x => x.Priority!.Value)
            .IsInEnum()
            .When(x => x.Priority.HasValue);

        RuleFor(x => x.AssigneeId!)
            .NotEmpty()
            .When(x => x.AssigneeId is not null);   // if sent, must be non-empty

        // Existence is checked in the controller - the validator has no DB access.
        RuleFor(x => x.CategoryId!)
            .NotEmpty()
            .When(x => x.CategoryId is not null);
    }
}
