using FluentValidation;

namespace backend.DTOs.Tickets;

public sealed class CreateTicketDtoValidator : AbstractValidator<CreateTicketDto>
{
    public CreateTicketDtoValidator()
    {
        RuleFor(x => x.Subject)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(4000);

        RuleFor(x => x.Priority)
            .IsInEnum();

        RuleFor(x => x.CategoryId)
            .NotEmpty();

        // Whether the id names a real, assignable person is a database question the
        // controller answers; this only rules out an empty string.
        RuleFor(x => x.AssigneeId!)
            .NotEmpty()
            .When(x => x.AssigneeId is not null);
    }
}
