using AccountingInventory.Application.Purchases.Products.Commands.CreateProduct;
using FluentValidation;

namespace AccountingInventory.Application.Purchases.Products.Commands.BulkCreateProducts;

public sealed class BulkCreateProductsCommandValidator : AbstractValidator<BulkCreateProductsCommand>
{
    public BulkCreateProductsCommandValidator()
    {
        RuleFor(x => x.Products).NotEmpty().WithMessage("Add at least one product.");
        RuleFor(x => x.Products).Must(products => products is { Count: <= 500 })
            .WithMessage("A single bulk purchase can contain at most 500 products.");
        RuleForEach(x => x.Products).SetValidator(new CreateProductCommandValidator());
    }
}
