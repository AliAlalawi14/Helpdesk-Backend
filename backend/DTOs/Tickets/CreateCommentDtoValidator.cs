using FluentValidation;

namespace backend.DTOs.Tickets;

public sealed class CreateCommentDtoValidator : AbstractValidator<CreateCommentDto>
{
    public CreateCommentDtoValidator()
    {
        RuleFor(x => x.Body)
            .NotEmpty()
            .MaximumLength(4000);
    }
}
