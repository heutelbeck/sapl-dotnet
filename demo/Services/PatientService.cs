using System.Text.Json;
using Sapl.Core.Authorization;
using Sapl.Core.Constraints;
using Sapl.Core.Enforcement;
using Sapl.Demo.Data;

namespace Sapl.Demo.Services;

public sealed class PatientService
{
    private readonly EnforcementEngine _engine;
    private readonly ILogger<PatientService> _logger;

    public PatientService(EnforcementEngine engine, ILogger<PatientService> logger)
    {
        _engine = engine;
        _logger = logger;
    }

    public async Task<object?> ListPatients(CancellationToken ct = default)
    {
        var sub = AuthorizationSubscription.Create("anonymous", "service:listPatients", "patients");
        var result = await _engine.PreEnforceAsync(sub, ct);
        if (!result.IsPermitted)
            throw new AccessDeniedException("Access denied.");

        return PatientData.Patients.Select(p => new
        {
            p.Id,
            p.Name,
            p.Ssn,
            p.Diagnosis,
            p.Classification,
        }).ToArray();
    }

    public async Task<object?> FindPatient(string name, CancellationToken ct = default)
    {
        var sub = AuthorizationSubscription.Create("anonymous", "service:findPatient", "patient");
        var result = await _engine.PreEnforceAsync(sub, ct);
        if (!result.IsPermitted)
            throw new AccessDeniedException("Access denied.");

        return PatientData.Patients
            .Where(p => p.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
            .Select(p => new { p.Id, p.Name, p.Ssn, p.Diagnosis, p.Classification })
            .ToArray();
    }

    public async Task<object?> SearchPatients(string query, CancellationToken ct = default)
    {
        var sub = AuthorizationSubscription.Create("anonymous", "service:searchPatients", "patientSearch");
        var result = await _engine.PreEnforceAsync(sub, ct);
        if (!result.IsPermitted)
            throw new AccessDeniedException("Access denied.");

        var patients = PatientData.Patients
            .Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || p.Diagnosis.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Select(p => new { p.Id, p.Name, p.Ssn, p.Diagnosis, p.Classification })
            .ToArray();

        if (result.Bundle is null)
            return patients;

        var filtered = result.Bundle.HandleAllOnNextConstraints(patients);
        result.Bundle.CheckFailedObligations();
        return filtered;
    }

    public async Task<object?> GetPatientDetail(string id, CancellationToken ct = default)
    {
        var patient = PatientData.Patients.FirstOrDefault(p => p.Id == id);
        if (patient is null)
            return null;

        var data = new { patient.Id, patient.Name, patient.Ssn, patient.Diagnosis, patient.Classification };
        var resource = new { type = "patientDetail", data };
        var sub = AuthorizationSubscription.Create("anonymous", "service:getPatientDetail", resource);

        var postResult = await _engine.PostEnforceAsync(sub, (object)data, ct);
        if (!postResult.IsPermitted)
            throw new AccessDeniedException("Access denied.");

        return postResult.Value;
    }

    public async Task<object?> GetPatientSummary(string id, CancellationToken ct = default)
    {
        var patient = PatientData.Patients.FirstOrDefault(p => p.Id == id);
        if (patient is null)
            return null;

        var sub = AuthorizationSubscription.Create("anonymous", "service:getPatientSummary", "patientSummary");
        var result = await _engine.PreEnforceAsync(sub, ct);
        if (!result.IsPermitted)
            throw new AccessDeniedException("Access denied.");

        var data = new
        {
            patient.Id,
            patient.Name,
            patient.Ssn,
            patient.Diagnosis,
            patient.Classification,
            insurance = "INS-9876-XYZ",
        };

        if (result.Bundle is null)
            return data;

        var processed = result.Bundle.HandleAllOnNextConstraints(data);
        result.Bundle.CheckFailedObligations();
        return processed;
    }

    public async Task<object?> Transfer(double amount, CancellationToken ct = default)
    {
        var sub = AuthorizationSubscription.Create("anonymous", "service:transfer", "account");
        var result = await _engine.PreEnforceAsync(sub, ct);
        if (!result.IsPermitted)
            throw new AccessDeniedException("Access denied.");

        var finalAmount = amount;
        if (result.Bundle is not null)
        {
            var miContext = new Core.Constraints.Api.MethodInvocationContext(
                [amount], "Transfer", "PatientService", null);
            result.Bundle.HandleMethodInvocationHandlers(miContext);
            result.Bundle.CheckFailedObligations();
            if (miContext.Args[0] is double capped)
            {
                finalAmount = capped;
            }
        }

        return new { transferred = finalAmount };
    }
}
