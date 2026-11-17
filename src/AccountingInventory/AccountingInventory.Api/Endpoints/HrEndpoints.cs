using AccountingInventory.Api.Endpoints.Hr.Requests;
using AccountingInventory.Application.Common.Models;
using AccountingInventory.Application.Hr.Dashboard.Models;
using AccountingInventory.Application.Hr.Dashboard.Queries.GetHrDashboardMetrics;
using AccountingInventory.Application.Hr.Departments.Commands.CreateDepartment;
using AccountingInventory.Application.Hr.Departments.Models;
using AccountingInventory.Application.Hr.Departments.Queries.GetDepartments;
using AccountingInventory.Application.Hr.Employees.Commands.CreateEmployee;
using AccountingInventory.Application.Hr.Employees.Commands.DeleteEmployee;
using AccountingInventory.Application.Hr.Employees.Commands.UpdateEmployee;
using AccountingInventory.Application.Hr.Employees.Models;
using AccountingInventory.Application.Hr.Employees.Queries.GetEmployeeById;
using AccountingInventory.Application.Hr.Employees.Queries.GetEmployees;
using AccountingInventory.Application.Hr.Leaves.Commands.AllocateLeaveBalance;
using AccountingInventory.Application.Hr.Leaves.Commands.ApplyLeaveRequest;
using AccountingInventory.Application.Hr.Leaves.Commands.CreateLeaveType;
using AccountingInventory.Application.Hr.Leaves.Commands.ReviewLeaveRequest;
using AccountingInventory.Application.Hr.Leaves.Models;
using AccountingInventory.Application.Hr.Leaves.Queries.GetLeaveBalances;
using AccountingInventory.Application.Hr.Leaves.Queries.GetLeaveRequests;
using AccountingInventory.Application.Hr.Leaves.Queries.GetLeaveTypes;
using AccountingInventory.Application.Hr.Masters.Commands.CreateMaster;
using AccountingInventory.Application.Hr.Masters.Models;
using AccountingInventory.Application.Hr.Masters.Queries.GetMasters;
using AccountingInventory.Application.Hr.Payroll.Commands.DisbursePayrollRun;
using AccountingInventory.Application.Hr.Payroll.Commands.MarkPayslipPaid;
using AccountingInventory.Application.Hr.Payroll.Commands.ProcessPayrollRun;
using AccountingInventory.Application.Hr.Payroll.Commands.SaveSalaryStructure;
using AccountingInventory.Application.Hr.Payroll.Models;
using AccountingInventory.Application.Hr.Payroll.Queries.GetPayrollRuns;
using AccountingInventory.Application.Hr.Payroll.Queries.GetPayslipsByRun;
using AccountingInventory.Application.Hr.Payroll.Queries.GetSalaryStructure;
using AccountingInventory.Domain.Entities;
using BuildingBlocks.WebDefaults;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace AccountingInventory.Api.Endpoints;

public static class HrEndpoints
{
    public static RouteGroupBuilder MapHrEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/hr")
            .WithTags("Smart HR + Payroll + Leave")
            .RequireAuthorization("AuthenticatedUser")
            .AddEndpointFilter<TenantHeaderEndpointFilter>()
            .WithMetadata(new RequiresTenantIdHeaderAttribute());

        #region 1. Departments

        group.MapGet("/departments", async (ISender sender, CancellationToken ct) =>
        {
            var depts = await sender.Send(new GetDepartmentsQuery(), ct);
            return Results.Ok(depts);
        }).WithName("GetHrDepartments");

        group.MapPost("/departments", async ([FromBody] CreateDepartmentRequest req, ISender sender, CancellationToken ct) =>
        {
            var command = new CreateDepartmentCommand(req.Name, req.Code, req.Description, req.HeadOfDepartment);
            var result = await sender.Send(command, ct);
            return Results.Created($"/api/hr/departments/{result.Id}", result);
        }).WithName("CreateHrDepartment");

        #endregion

        #region 2. Employees

        group.MapGet("/employees", async (
            [FromQuery] string? search,
            [FromQuery] string? department,
            [FromQuery] EmploymentStatus? status,
            [FromQuery] int? page,
            [FromQuery] int? pageSize,
            ISender sender,
            CancellationToken ct) =>
        {
            var query = new GetEmployeesQuery(search, department, status, page, pageSize);
            var paged = await sender.Send(query, ct);
            return Results.Ok(paged);
        }).WithName("GetHrEmployees");

        group.MapGet("/employees/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var emp = await sender.Send(new GetEmployeeByIdQuery(id), ct);
            return Results.Ok(emp);
        }).WithName("GetHrEmployeeById");

        group.MapPost("/employees", async ([FromBody] CreateEmployeeRequest req, ISender sender, CancellationToken ct) =>
        {
            // Resolve joining date defensively from either DateOnly or ISO string
            DateOnly effectiveJoiningDate;
            if (req.DateOfJoining.HasValue)
            {
                effectiveJoiningDate = req.DateOfJoining.Value;
            }
            else if (!string.IsNullOrWhiteSpace(req.JoiningDate) && DateOnly.TryParse(req.JoiningDate, out var parsedJoining))
            {
                effectiveJoiningDate = parsedJoining;
            }
            else
            {
                effectiveJoiningDate = DateOnly.FromDateTime(DateTime.UtcNow);
            }

            // Resolve employee status
            var effectiveStatus = req.EmploymentStatus != default
                ? req.EmploymentStatus
                : (!string.IsNullOrWhiteSpace(req.Status) && Enum.TryParse<EmploymentStatus>(req.Status, true, out var parsedStatus) ? parsedStatus : EmploymentStatus.Active);

            var effectiveSalary = req.MonthlyBaseSalary ?? req.BaseSalary ?? 0m;
            var effectiveDepartment = !string.IsNullOrWhiteSpace(req.Department) ? req.Department : req.DepartmentId;
            var effectivePan = !string.IsNullOrWhiteSpace(req.PanNumber) ? req.PanNumber : req.Pan;
            var effectiveAadhaar = !string.IsNullOrWhiteSpace(req.AadhaarNumber) ? req.AadhaarNumber : req.Aadhaar;
            var effectiveUan = !string.IsNullOrWhiteSpace(req.UanNumber) ? req.UanNumber : req.Uan;
            var effectiveBankAcc = !string.IsNullOrWhiteSpace(req.BankAccountNumber) ? req.BankAccountNumber : null;
            var effectiveBankName = !string.IsNullOrWhiteSpace(req.BankName) ? req.BankName : null;
            var effectiveIfsc = !string.IsNullOrWhiteSpace(req.BankIfscCode) ? req.BankIfscCode : req.BankIfsc;
            var effectiveSalaryType = !string.IsNullOrWhiteSpace(req.SalaryType) ? req.SalaryType : "Monthly";

            var command = new CreateEmployeeCommand(
                req.EmployeeCode,
                req.FirstName,
                req.LastName,
                req.Phone,
                req.Designation,
                effectiveJoiningDate,
                req.Email,
                effectiveDepartment,
                req.EmploymentType,
                effectiveStatus,
                effectiveSalary,
                effectiveSalaryType,
                req.ReportsToEmployeeId,
                req.ReportsToEmployeeName,
                req.DateOfBirth,
                req.Gender,
                effectivePan,
                effectiveAadhaar,
                effectiveUan,
                effectiveBankAcc,
                effectiveBankName,
                effectiveIfsc,
                req.Address,
                req.EmergencyContactName,
                req.EmergencyContactPhone,
                req.Notes);

            var result = await sender.Send(command, ct);
            return Results.Created($"/api/hr/employees/{result.Id}", result);
        }).WithName("CreateHrEmployee");

        group.MapPut("/employees/{id:guid}", async (Guid id, [FromBody] UpdateEmployeeRequest req, ISender sender, CancellationToken ct) =>
        {
            DateOnly effectiveJoiningDate;
            if (req.DateOfJoining.HasValue)
            {
                effectiveJoiningDate = req.DateOfJoining.Value;
            }
            else if (!string.IsNullOrWhiteSpace(req.JoiningDate) && DateOnly.TryParse(req.JoiningDate, out var parsedJoining))
            {
                effectiveJoiningDate = parsedJoining;
            }
            else
            {
                effectiveJoiningDate = DateOnly.FromDateTime(DateTime.UtcNow);
            }

            var effectiveStatus = req.EmploymentStatus != default
                ? req.EmploymentStatus
                : (!string.IsNullOrWhiteSpace(req.Status) && Enum.TryParse<EmploymentStatus>(req.Status, true, out var parsedStatus) ? parsedStatus : EmploymentStatus.Active);

            var effectiveSalary = req.MonthlyBaseSalary ?? req.BaseSalary ?? 0m;
            var effectiveDepartment = !string.IsNullOrWhiteSpace(req.Department) ? req.Department : req.DepartmentId;
            var effectivePan = !string.IsNullOrWhiteSpace(req.PanNumber) ? req.PanNumber : req.Pan;
            var effectiveAadhaar = !string.IsNullOrWhiteSpace(req.AadhaarNumber) ? req.AadhaarNumber : req.Aadhaar;
            var effectiveUan = !string.IsNullOrWhiteSpace(req.UanNumber) ? req.UanNumber : req.Uan;
            var effectiveBankAcc = !string.IsNullOrWhiteSpace(req.BankAccountNumber) ? req.BankAccountNumber : null;
            var effectiveBankName = !string.IsNullOrWhiteSpace(req.BankName) ? req.BankName : null;
            var effectiveIfsc = !string.IsNullOrWhiteSpace(req.BankIfscCode) ? req.BankIfscCode : req.BankIfsc;
            var effectiveSalaryType = !string.IsNullOrWhiteSpace(req.SalaryType) ? req.SalaryType : "Monthly";

            var command = new UpdateEmployeeCommand(
                id,
                req.FirstName,
                req.LastName,
                req.Phone,
                req.Designation,
                effectiveJoiningDate,
                req.Email,
                effectiveDepartment,
                req.EmploymentType,
                effectiveStatus,
                effectiveSalary,
                effectiveSalaryType,
                req.ReportsToEmployeeId,
                req.ReportsToEmployeeName,
                req.DateOfBirth,
                req.Gender,
                effectivePan,
                effectiveAadhaar,
                effectiveUan,
                effectiveBankAcc,
                effectiveBankName,
                effectiveIfsc,
                req.Address,
                req.EmergencyContactName,
                req.EmergencyContactPhone,
                req.Notes);

            var result = await sender.Send(command, ct);
            return Results.Ok(result);
        }).WithName("UpdateHrEmployee");

        group.MapDelete("/employees/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeleteEmployeeCommand(id), ct);
            return Results.NoContent();
        }).WithName("DeleteHrEmployee");

        #endregion

        #region 3. Leave Management

        group.MapGet("/leaves/types", async (ISender sender, CancellationToken ct) =>
        {
            var types = await sender.Send(new GetLeaveTypesQuery(), ct);
            return Results.Ok(types);
        }).WithName("GetHrLeaveTypes");

        group.MapPost("/leaves/types", async ([FromBody] CreateLeaveTypeRequest req, ISender sender, CancellationToken ct) =>
        {
            var command = new CreateLeaveTypeCommand(req.Name, req.Code, req.DaysAllowedPerYear, req.IsPaid, req.IsCarryForward, req.Description);
            var result = await sender.Send(command, ct);
            return Results.Created($"/api/hr/leaves/types/{result.Id}", result);
        }).WithName("CreateHrLeaveType");

        group.MapGet("/leaves/balances", async ([FromQuery] Guid? employeeId, [FromQuery] int? year, ISender sender, CancellationToken ct) =>
        {
            var balances = await sender.Send(new GetLeaveBalancesQuery(employeeId, year), ct);
            return Results.Ok(balances);
        }).WithName("GetHrLeaveBalances");

        group.MapPost("/leaves/balances/{employeeId:guid}/allocate", async (
            Guid employeeId,
            [FromBody] AllocateLeaveBalanceRequest req,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new AllocateLeaveBalanceCommand(employeeId, req.LeaveTypeId, req.Year, req.TotalDays);
            var result = await sender.Send(command, ct);
            return Results.Ok(result);
        }).WithName("AllocateHrLeaveBalance");

        group.MapGet("/leaves/requests", async ([FromQuery] Guid? employeeId, [FromQuery] LeaveRequestStatus? status, ISender sender, CancellationToken ct) =>
        {
            var requests = await sender.Send(new GetLeaveRequestsQuery(employeeId, status), ct);
            return Results.Ok(requests);
        }).WithName("GetHrLeaveRequests");

        group.MapPost("/leaves/requests", async ([FromBody] ApplyLeaveRequest req, ISender sender, CancellationToken ct) =>
        {
            var command = new ApplyLeaveRequestCommand(req.EmployeeId, req.LeaveTypeId, req.StartDate, req.EndDate, req.TotalDays, req.Reason);
            var result = await sender.Send(command, ct);
            return Results.Created($"/api/hr/leaves/requests/{result.Id}", result);
        }).WithName("ApplyHrLeaveRequest");

        group.MapPost("/leaves/requests/{id:guid}/approve", async (
            Guid id,
            [FromBody] LeaveDecisionRequest req,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new ReviewLeaveRequestCommand(id, IsApproved: true, Approver: "HR Manager", Comment: req.Comment);
            var result = await sender.Send(command, ct);
            return Results.Ok(result);
        }).WithName("ApproveHrLeaveRequest");

        group.MapPost("/leaves/requests/{id:guid}/reject", async (
            Guid id,
            [FromBody] LeaveDecisionRequest req,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new ReviewLeaveRequestCommand(id, IsApproved: false, Approver: "HR Manager", Comment: req.Comment);
            var result = await sender.Send(command, ct);
            return Results.Ok(result);
        }).WithName("RejectHrLeaveRequest");

        #endregion

        #region 4. Payroll

        group.MapGet("/payroll/salary-structures/{employeeId:guid}", async (Guid employeeId, ISender sender, CancellationToken ct) =>
        {
            var structure = await sender.Send(new GetSalaryStructureQuery(employeeId), ct);
            if (structure is null)
                return Results.NotFound(new { message = "Salary structure not defined for this employee." });

            return Results.Ok(structure);
        }).WithName("GetHrSalaryStructure");

        group.MapPost("/payroll/salary-structures/{employeeId:guid}", async (
            Guid employeeId,
            [FromBody] SaveSalaryStructureRequest req,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new SaveSalaryStructureCommand(
                employeeId, req.BasicSalary, req.Hra, req.DearnessAllowance, req.ConveyanceAllowance,
                req.SpecialAllowance, req.MedicalAllowance, req.PerformanceBonus, req.ProvidentFund,
                req.Esi, req.ProfessionalTax, req.TdsTax, req.OtherDeductions, req.PaymentMode, req.Remarks);

            var result = await sender.Send(command, ct);
            return Results.Ok(result);
        }).WithName("SaveHrSalaryStructure");

        group.MapGet("/payroll/runs", async ([FromQuery] int? year, ISender sender, CancellationToken ct) =>
        {
            var runs = await sender.Send(new GetPayrollRunsQuery(year), ct);
            return Results.Ok(runs);
        }).WithName("GetHrPayrollRuns");

        group.MapGet("/payroll/runs/{id:guid}/payslips", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var payslips = await sender.Send(new GetPayslipsByRunQuery(id), ct);
            return Results.Ok(payslips);
        }).WithName("GetHrPayrollRunPayslips");

        group.MapPost("/payroll/runs/process", async ([FromBody] ProcessPayrollRequest req, ISender sender, CancellationToken ct) =>
        {
            var command = new ProcessPayrollRunCommand(req.Month, req.Year, req.Notes);
            var result = await sender.Send(command, ct);
            return Results.Created($"/api/hr/payroll/runs/{result.Id}", result);
        }).WithName("ProcessHrPayrollRun");

        group.MapPost("/payroll/runs/{id:guid}/disburse", async (
            Guid id,
            [FromBody] DisbursePayrollRequest req,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new DisbursePayrollRunCommand(id, req.PaymentDate, req.Notes);
            var result = await sender.Send(command, ct);
            return Results.Ok(result);
        }).WithName("DisburseHrPayrollRun");

        group.MapPost("/payroll/payslips/{id:guid}/mark-paid", async (
            Guid id,
            [FromBody] MarkPaidRequest req,
            ISender sender,
            CancellationToken ct) =>
        {
            var command = new MarkPayslipPaidCommand(id, req.PaymentMode, req.PaymentReference);
            var result = await sender.Send(command, ct);
            return Results.Ok(result);
        }).WithName("MarkHrPayslipPaid");

        #endregion

        #region 5. Dashboard Metrics

        group.MapGet("/metrics", async (ISender sender, CancellationToken ct) =>
        {
            var metrics = await sender.Send(new GetHrDashboardMetricsQuery(), ct);
            return Results.Ok(metrics);
        }).WithName("GetHrMetrics");

        group.MapGet("/dashboard/metrics", async (ISender sender, CancellationToken ct) =>
        {
            var metrics = await sender.Send(new GetHrDashboardMetricsQuery(), ct);
            return Results.Ok(metrics);
        }).WithName("GetHrDashboardMetrics");

        #endregion

        #region 6. Masters (Job Title, Employment Type, Status, Salary Type, Leave Category)

        group.MapGet("/masters", async ([FromQuery] string? category, ISender sender, CancellationToken ct) =>
        {
            var items = await sender.Send(new GetHrMastersQuery(category), ct);
            return Results.Ok(items);
        }).WithName("GetHrMasters");

        group.MapPost("/masters", async ([FromBody] CreateHrMasterRequest req, ISender sender, CancellationToken ct) =>
        {
            var command = new CreateHrMasterCommand(req.Category, req.Name, req.Code, req.Description, req.SortOrder, req.IsDefault);
            var result = await sender.Send(command, ct);
            return Results.Created($"/api/hr/masters/{result.Id}", result);
        }).WithName("CreateHrMaster");

        #endregion

        #region 7. Statutory & Formula Calculations

        group.MapGet("/payroll/calculate-statutory", ([FromQuery] decimal salary, [FromQuery] bool isCtc) =>
        {
            var breakdown = IndianPayrollCalculator.Calculate(salary, isCtc);
            return Results.Ok(breakdown);
        }).WithName("CalculateStatutoryBreakdown");

        group.MapGet("/leaves/calculate-working-days", ([FromQuery] DateOnly startDate, [FromQuery] DateOnly endDate) =>
        {
            var (workingDays, weekends, holidays) = LeaveWorkingDaysCalculator.CalculateNetLeaveDays(startDate, endDate);
            return Results.Ok(new { startDate, endDate, workingDays, weekends, holidays });
        }).WithName("CalculateNetLeaveDays");

        #endregion

        return group;
    }
}
