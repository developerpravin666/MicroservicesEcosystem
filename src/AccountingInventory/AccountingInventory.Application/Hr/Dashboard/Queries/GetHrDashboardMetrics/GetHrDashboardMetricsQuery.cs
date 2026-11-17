using AccountingInventory.Application.Hr.Dashboard.Models;
using MediatR;

namespace AccountingInventory.Application.Hr.Dashboard.Queries.GetHrDashboardMetrics;

public sealed record GetHrDashboardMetricsQuery : IRequest<HrDashboardMetricsDto>;
