using AccountingInventory.Application.Abstractions;
using AccountingInventory.Application.Common.Models;
using AccountingInventory.Application.GeneralLedger;
using AccountingInventory.Application.Sales.Products;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.Application.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AccountingInventory.Application.Sales.Invoices;

public sealed record CreateSalesInvoiceCommand(
    string CustomerName,
    string CustomerMobile,
    string CustomerAddress,
    string? CustomerEmail,
    DateOnly InvoiceDate,
    int PaymentTermsDays,
    string? Notes,
    IReadOnlyList<CreateSalesInvoiceLineCommandDto> Lines,
    CreateSalesInvoiceInitialPaymentDto? InitialPayment = null,
    string? CustomerGstin = null,
    string? PlaceOfSupplyStateCode = null,
    string? PlaceOfSupplyStateName = null,
    GstSupplyType? SupplyType = null) : IRequest<SalesInvoiceDetailsDto>;

public sealed class CreateSalesInvoiceCommandHandler(IAccountingInventoryDbContext db)
    : IRequestHandler<CreateSalesInvoiceCommand, SalesInvoiceDetailsDto>
{
    public async Task<SalesInvoiceDetailsDto> Handle(CreateSalesInvoiceCommand request, CancellationToken cancellationToken)
    {
        if (request.Lines == null || request.Lines.Count == 0)
            throw new ArgumentException("At least one invoice line item is required.");

        var seller = await db.CustomerBillSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);

        // Resolve Place of Supply and customer GST details
        var customerGstin = !string.IsNullOrWhiteSpace(request.CustomerGstin) ? request.CustomerGstin.Trim().ToUpperInvariant() : null;
        var posCode = GstStates.ExtractStateCode(request.PlaceOfSupplyStateCode)
            ?? GstStates.ExtractStateCode(customerGstin)
            ?? GstStates.ExtractStateCode(request.CustomerAddress);

        var posName = !string.IsNullOrWhiteSpace(request.PlaceOfSupplyStateName)
            ? request.PlaceOfSupplyStateName.Trim()
            : GstStates.GetStateName(posCode);

        // Determine Intra-State (CGST+SGST) vs Inter-State (IGST)
        var supplierState = seller?.StateCode ?? GstStates.ExtractStateCode(seller?.TaxRegistrationNumber) ?? GstStates.ExtractStateCode(seller?.CompanyAddress);
        var effectiveSupplyType = request.SupplyType ?? GstStates.DetermineSupplyType(supplierState, posCode);

        if (request.SupplyType.HasValue && supplierState != null && posCode != null
            && request.SupplyType.Value != GstStates.DetermineSupplyType(supplierState, posCode))
            throw new ConflictException("Supply type must match the supplier state and place of supply.");
        if (!string.IsNullOrWhiteSpace(seller?.TaxRegistrationNumber))
        {
            if (supplierState == null || posCode == null) throw new ConflictException("Configure supplier state and choose a place of supply before issuing a GST invoice.");
            if (request.Lines.Any(l => string.IsNullOrWhiteSpace(l.HsnSac))) throw new ConflictException("HSN/SAC is required on GST invoice lines.");
        }
        var customer = await CustomerResolver.GetOrCreateAsync(
            db,
            request.CustomerName,
            request.CustomerMobile,
            request.CustomerAddress,
            request.CustomerEmail,
            cancellationToken,
            posCode,
            posName,
            customerGstin);

        // Process serialized products in the lines
        var serializedProductIds = request.Lines
            .Where(l => l.ProductId.HasValue && l.ProductId.Value != Guid.Empty)
            .Select(l => l.ProductId!.Value)
            .Distinct()
            .ToList();

        var productsToRelieve = new List<Product>();

        if (serializedProductIds.Count > 0)
        {
            var foundProducts = await db.Products
                .Where(p => serializedProductIds.Contains(p.Id))
                .ToListAsync(cancellationToken);

            foreach (var prodId in serializedProductIds)
            {
                var prod = foundProducts.FirstOrDefault(p => p.Id == prodId)
                    ?? throw new NotFoundException($"Product with ID '{prodId}' was not found.");

                if (!prod.IsActive || prod.IsSold)
                    throw new ConflictException($"Product '{prod.SerialNumber}' has already been sold or is inactive.");

                if (request.InvoiceDate < prod.PurchaseDate)
                    throw new ConflictException($"Sale date {request.InvoiceDate} cannot precede purchase date {prod.PurchaseDate} for product '{prod.SerialNumber}'.");

                var previousInvoiceIds = await db.SalesInvoiceLines.Where(l => l.ProductId == prod.Id).Select(l => l.SalesInvoiceId).ToArrayAsync(cancellationToken);
                var previousSaleIds = await db.SalesProducts.Where(s => s.ProductId == prod.Id.ToString()).Select(s => s.Id).ToArrayAsync(cancellationToken);
                if (await db.InvoiceCorrections.AnyAsync(n => n.Kind == "Sale" && (previousInvoiceIds.Contains(n.SourceId) || previousSaleIds.Contains(n.SourceId))
                    && n.NoteDate > request.InvoiceDate, cancellationToken))
                    throw new ConflictException("Resale cannot precede the previous customer return.");

                prod.MarkSold();
                productsToRelieve.Add(prod);
            }
        }

        if (request.Lines.Count(l => l.ItemType == InvoiceItemType.SerializedProduct) != serializedProductIds.Count)
            throw new ConflictException("Select a different inventory product for every serialized line.");
        var taxes = await db.Taxes.Where(t => t.IsActive).ToDictionaryAsync(t => t.Id, cancellationToken);
        foreach (var line in request.Lines)
        {
            if (line.TaxId is { } taxId && taxId != Guid.Empty && !taxes.ContainsKey(taxId))
                throw new ConflictException("The selected tax is inactive or does not exist.");
        }

        var skuIds = request.Lines.Where(l => l.SkuId.HasValue).Select(l => l.SkuId!.Value).Distinct().ToArray();
        var skus = await db.StockSkus.Where(s => skuIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, cancellationToken);
        if (skus.Count != skuIds.Length) throw new NotFoundException("Selected SKU does not exist.");
        foreach (var line in request.Lines.Where(l => l.ItemType == InvoiceItemType.StandardProduct))
            if (!line.SkuId.HasValue) throw new ConflictException("Select stocked SKU for an accessory line; use Service for non-stock charges.");
        var billNumber = await db.GenerateSalesBillNumberAsync(request.InvoiceDate.Year, cancellationToken);

        var drafts = request.Lines.Select(l =>
        {
            if (l.SkuId.HasValue) {
                var sku = skus[l.SkuId.Value];
                l = l with { ItemDescription = sku.Name, HsnSac = sku.HsnSac, UnitOfMeasure = sku.UnitOfMeasure };
            }
            var product = l.ProductId.HasValue ? productsToRelieve.SingleOrDefault(p => p.Id == l.ProductId.Value) : null;
            var tax = l.TaxId.HasValue && taxes.TryGetValue(l.TaxId.Value, out var selectedTax) ? selectedTax : null;
            if (tax is null && (l.CgstRate != 0 || l.SgstRate != 0 || l.IgstRate != 0))
                throw new ConflictException("Select an active tax rate instead of supplying arbitrary rates.");
            l = l with { CgstRate = tax?.Cgst ?? 0m, SgstRate = tax?.Sgst ?? 0m, IgstRate = (tax?.Cgst ?? 0m) + (tax?.Sgst ?? 0m),
                SerialNumber = product?.SerialNumber, SerialNumber1 = product?.SerialNumber1 };
            decimal cgstRate = 0m;
            decimal sgstRate = 0m;
            decimal igstRate = 0m;

            if (effectiveSupplyType == GstSupplyType.InterState)
            {
                // Inter-state supply -> entire tax applies as IGST
                igstRate = l.IgstRate > 0 ? l.IgstRate : (l.CgstRate + l.SgstRate);
                cgstRate = 0m;
                sgstRate = 0m;
            }
            else
            {
                // Intra-state supply -> split tax equally into CGST + SGST
                if (l.CgstRate == 0 && l.SgstRate == 0 && l.IgstRate > 0)
                {
                    cgstRate = decimal.Round(l.IgstRate / 2m, 2, MidpointRounding.AwayFromZero);
                    sgstRate = decimal.Round(l.IgstRate / 2m, 2, MidpointRounding.AwayFromZero);
                    igstRate = 0m;
                }
                else
                {
                    cgstRate = l.CgstRate;
                    sgstRate = l.SgstRate;
                    igstRate = 0m;
                }
            }

            return new SalesInvoiceLineDraft(
                l.ItemType,
                l.ProductId,
                l.ItemDescription,
                l.SerialNumber,
                l.SerialNumber1,
                l.Quantity,
                l.UnitPrice,
                l.Discount,
                l.TaxId,
                cgstRate,
                sgstRate,
                igstRate, l.HsnSac, l.UnitOfMeasure, l.SkuId);
        }).ToList();

        var invoice = SalesInvoice.Create(
            billNumber,
            customer.Id,
            request.InvoiceDate,
            request.PaymentTermsDays,
            request.Notes,
            drafts,
            effectiveSupplyType,
            posCode,
            posName,
            customerGstin);

        db.SalesInvoices.Add(invoice);

        // General Ledger posting for the multi-line invoice
        var ledgerAccounts = await LedgerPosting.EnsureSystemAccountsAsync(db, cancellationToken);
        decimal skuCost = 0;
        foreach (var line in invoice.Lines.Where(l => l.SkuId.HasValue)) {
            var sku = skus[line.SkuId!.Value]; var cost = sku.Issue(line.Quantity, invoice.InvoiceDate); skuCost += cost;
            db.SkuMovements.Add(SkuMovement.Create(sku.Id,invoice.InvoiceDate,"Sale",invoice.Id,-line.Quantity,-cost,invoice.BillNumber));
        }
        var saleJournal = LedgerPosting.ForSalesInvoice(invoice, productsToRelieve, ledgerAccounts, skuCost);
        if (saleJournal is not null)
        {
            LedgerPosting.Add(db, saleJournal);
        }

        await LedgerPosting.EnsurePeriodOpenAsync(db, invoice.InvoiceDate, cancellationToken);

        SalesInvoiceReceipt? receipt = null;
        if (request.InitialPayment != null && request.InitialPayment.Amount > 0)
        {
            if (request.InitialPayment.PaymentDate < invoice.InvoiceDate) throw new ConflictException("Payment date cannot precede invoice date.");
            invoice.RecordPayment(request.InitialPayment.Amount);
            receipt = SalesInvoiceReceipt.Create(
                invoice.Id,
                request.InitialPayment.Amount,
                request.InitialPayment.PaymentMode,
                request.InitialPayment.PaymentDate,
                request.InitialPayment.ReferenceNumber,
                request.InitialPayment.Note);

            db.SalesInvoiceReceipts.Add(receipt);
            LedgerPosting.Add(db, LedgerPosting.ForSalesReceipt(receipt, ledgerAccounts));
            await LedgerPosting.EnsurePeriodOpenAsync(db, receipt.PaymentDate, cancellationToken);
        }

        // Capture immutable invoice snapshot for printing and history
        var itemsSummary = string.Join("; ", invoice.Lines.Select(l => $"{l.ItemDescription} (x{l.Quantity})"));
        var serialsSummary = string.Join(", ", invoice.Lines.Where(l => !string.IsNullOrWhiteSpace(l.SerialNumber)).Select(l => l.SerialNumber));

        db.InvoiceSnapshots.Add(InvoiceSnapshot.Capture(
            "Sale",
            invoice.Id,
            customer.Name,
            customer.Mobile,
            customer.Address,
            customer.Email,
            itemsSummary,
            serialsSummary,
            JsonSerializer.Serialize(new
            {
                BillNumber = invoice.BillNumber,
                SaleDate = invoice.InvoiceDate,
                DueDate = invoice.DueDate,
                SupplyType = invoice.SupplyType,
                PlaceOfSupplyStateCode = invoice.PlaceOfSupplyStateCode,
                PlaceOfSupplyStateName = invoice.PlaceOfSupplyStateName,
                CustomerGstin = invoice.CustomerGstin,
                SubTotal = invoice.SubTotal,
                Discount = invoice.Discount,
                TaxableAmount = invoice.TaxableAmount,
                CgstAmount = invoice.CgstAmount,
                SgstAmount = invoice.SgstAmount,
                IgstAmount = invoice.IgstAmount,
                TotalAmount = invoice.TotalAmount,
                Seller = seller,
                Lines = invoice.Lines.Select(l => new
                {
                    l.LineNumber, l.HsnSac, l.UnitOfMeasure,
                    l.ItemDescription,
                    l.SerialNumber,
                    l.SerialNumber1,
                    l.Quantity,
                    l.UnitPrice,
                    l.Discount,
                    l.CgstRate,
                    l.SgstRate,
                    l.IgstRate,
                    l.TaxableAmount,
                    l.CgstAmount,
                    l.SgstAmount,
                    l.IgstAmount,
                    l.TotalAmount
                })
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web))));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Stock changed while the sale was being saved. Refresh and try again.");
        }

        var summary = new SalesInvoiceSummaryDto(
            invoice.Id,
            invoice.BillNumber,
            customer.Id,
            customer.Name,
            customer.Mobile,
            customer.Address,
            customer.Email,
            invoice.InvoiceDate,
            invoice.PaymentTermsDays,
            invoice.DueDate,
            invoice.Lines.Count,
            invoice.SubTotal,
            invoice.Discount,
            invoice.TaxableAmount,
            invoice.CgstAmount,
            invoice.SgstAmount,
            invoice.IgstAmount,
            invoice.TotalAmount,
            invoice.AmountPaid,
            invoice.Balance,
            invoice.PaymentStatus,
            invoice.Notes,
            invoice.SupplyType,
            invoice.PlaceOfSupplyStateCode,
            invoice.PlaceOfSupplyStateName,
            invoice.CustomerGstin);

        var lineDtos = invoice.Lines.Select(l => new SalesInvoiceLineDto(
            l.Id,
            l.LineNumber,
            l.ItemType,
            l.ProductId,
            l.ItemDescription,
            l.SerialNumber,
            l.SerialNumber1,
            l.Quantity,
            l.UnitPrice,
            l.Discount,
            l.TaxId,
            l.CgstRate,
            l.SgstRate,
            l.IgstRate,
            l.TaxableAmount,
            l.CgstAmount,
            l.SgstAmount,
            l.IgstAmount,
            l.TotalAmount, l.HsnSac, l.UnitOfMeasure, l.SkuId)).ToList();

        var paymentDtos = receipt is not null
            ? new List<SalesInvoiceReceiptDto>
            {
                new(receipt.Id, receipt.Amount, receipt.PaymentMode, receipt.PaymentDate, receipt.ReferenceNumber, receipt.Note)
            }
            : new List<SalesInvoiceReceiptDto>();

        return new SalesInvoiceDetailsDto(summary, lineDtos, paymentDtos);
    }
}

