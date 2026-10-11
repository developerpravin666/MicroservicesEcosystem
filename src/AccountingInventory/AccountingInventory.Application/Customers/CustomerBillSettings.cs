using AccountingInventory.Application.Abstractions;
using AccountingInventory.Domain.Entities;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AccountingInventory.Application.Customers;

public sealed record CustomerBillTemplate(
    string CompanyName,
    string CompanyAddress,
    string CompanyMobile,
    string? CompanyEmail,
    string? TaxRegistrationNumber,
    string BillTitle,
    string FooterNote,
    string PaperSize,
    bool ShowCustomerEmail,
    bool ShowSerialNumber,
    bool ShowDiscount,
    bool ShowPaymentHistory,
    bool ShowBalanceDue,
    string? StateCode = null,
    string? StateName = null);

public sealed record GetCustomerBillSettingsQuery() : IRequest<CustomerBillTemplate>;

public sealed class GetCustomerBillSettingsQueryHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<GetCustomerBillSettingsQuery, CustomerBillTemplate>
{
    public async Task<CustomerBillTemplate> Handle(GetCustomerBillSettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = await db.CustomerBillSettings.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == CustomerBillSettings.TenantSettingsId, cancellationToken);
        return settings is null
            ? new CustomerBillTemplate(string.Empty, string.Empty, string.Empty, null, null,
                "SALES INVOICE", "Thank you for your business.", "A4", true, true, true, true, true, null, null)
            : Map(settings);
    }

    internal static CustomerBillTemplate Map(CustomerBillSettings settings)
    {
        var code = settings.StateCode ?? GstStates.ExtractStateCode(settings.TaxRegistrationNumber) ?? GstStates.ExtractStateCode(settings.CompanyAddress);
        var name = settings.StateName ?? GstStates.GetStateName(code);
        return new(settings.CompanyName, settings.CompanyAddress, settings.CompanyMobile, settings.CompanyEmail,
            settings.TaxRegistrationNumber, settings.BillTitle, settings.FooterNote, settings.PaperSize,
            settings.ShowCustomerEmail, settings.ShowSerialNumber, settings.ShowDiscount,
            settings.ShowPaymentHistory, settings.ShowBalanceDue, code, name);
    }
}

public sealed record UpdateCustomerBillSettingsCommand(
    string CompanyName,
    string CompanyAddress,
    string CompanyMobile,
    string? CompanyEmail,
    string? TaxRegistrationNumber,
    string BillTitle,
    string FooterNote,
    string PaperSize,
    bool ShowCustomerEmail,
    bool ShowSerialNumber,
    bool ShowDiscount,
    bool ShowPaymentHistory,
    bool ShowBalanceDue,
    string? StateCode = null,
    string? StateName = null) : IRequest<CustomerBillTemplate>;

public sealed class UpdateCustomerBillSettingsCommandValidator : AbstractValidator<UpdateCustomerBillSettingsCommand>
{
    public UpdateCustomerBillSettingsCommandValidator()
    {
        RuleFor(command => command.CompanyName).NotEmpty().MaximumLength(200);
        RuleFor(command => command.CompanyAddress).NotEmpty().MaximumLength(500);
        RuleFor(command => command.CompanyMobile).NotEmpty().MaximumLength(20);
        RuleFor(command => command.CompanyEmail).EmailAddress().When(command => !string.IsNullOrWhiteSpace(command.CompanyEmail));
        RuleFor(command => command.TaxRegistrationNumber).MaximumLength(50);
        RuleFor(command => command.BillTitle).NotEmpty().MaximumLength(80);
        RuleFor(command => command.FooterNote).MaximumLength(500);
        RuleFor(command => command.PaperSize).Must(size => size is "A4" or "A5" or "Thermal80")
            .WithMessage("Paper size must be A4, A5, or Thermal80.");
    }
}

public sealed class UpdateCustomerBillSettingsCommandHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<UpdateCustomerBillSettingsCommand, CustomerBillTemplate>
{
    public async Task<CustomerBillTemplate> Handle(UpdateCustomerBillSettingsCommand request, CancellationToken cancellationToken)
    {
        var settings = await db.CustomerBillSettings
            .SingleOrDefaultAsync(item => item.Id == CustomerBillSettings.TenantSettingsId, cancellationToken);
        if (settings is null)
        {
            settings = CustomerBillSettings.Create(request.CompanyName, request.CompanyAddress, request.CompanyMobile,
                request.CompanyEmail, request.TaxRegistrationNumber, request.BillTitle, request.FooterNote,
                request.PaperSize, request.ShowCustomerEmail, request.ShowSerialNumber, request.ShowDiscount,
                request.ShowPaymentHistory, request.ShowBalanceDue, request.StateCode, request.StateName);
            db.CustomerBillSettings.Add(settings);
        }
        else
        {
            settings.Update(request.CompanyName, request.CompanyAddress, request.CompanyMobile,
                request.CompanyEmail, request.TaxRegistrationNumber, request.BillTitle, request.FooterNote,
                request.PaperSize, request.ShowCustomerEmail, request.ShowSerialNumber, request.ShowDiscount,
                request.ShowPaymentHistory, request.ShowBalanceDue, request.StateCode, request.StateName);
        }

        await db.SaveChangesAsync(cancellationToken);
        return GetCustomerBillSettingsQueryHandler.Map(settings);
    }
}
