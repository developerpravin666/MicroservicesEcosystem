using AccountingInventory.Application.Purchases.Products.Commands.UpdateProduct;
using FluentValidation;

namespace AccountingInventory.Application.Purchases.Products.Commands.BulkUpdateProducts;

public sealed class BulkUpdateProductsCommandValidator : AbstractValidator<BulkUpdateProductsCommand>
{
    public BulkUpdateProductsCommandValidator()
    {
        RuleFor(x => x.Products).NotEmpty().WithMessage("Select at least one product to update.");
        RuleFor(x => x.Products).Must(products => products is { Count: <= 500 })
            .WithMessage("A single bulk update can contain at most 500 products.");
        RuleForEach(x => x.Products).SetValidator(new UpdateProductCommandValidator());
    }
}
