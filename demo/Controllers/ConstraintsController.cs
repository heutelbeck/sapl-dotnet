using Microsoft.AspNetCore.Mvc;
using Sapl.AspNetCore.Attributes;
using Sapl.Demo.Data;
using Sapl.Demo.Handlers;

namespace Sapl.Demo.Controllers;

[ApiController]
[Route("api/constraints")]
public sealed class ConstraintsController : ControllerBase
{
    private readonly AuditTrailHandler _auditTrailHandler;

    public ConstraintsController(AuditTrailHandler auditTrailHandler)
    {
        _auditTrailHandler = auditTrailHandler;
    }

    [HttpGet("patient")]
    [PreEnforce(Action = "readPatient", Resource = "patient")]
    public IActionResult GetPatient()
    {
        return Ok(new
        {
            name = "Jane Doe",
            ssn = "123-45-6789",
            email = "jane.doe@example.com",
            diagnosis = "healthy",
        });
    }

    [HttpGet("patient-full")]
    [PreEnforce(Action = "readPatientFull", Resource = "patientFull")]
    public IActionResult GetPatientFull()
    {
        return Ok(new
        {
            name = "Jane Doe",
            ssn = "123-45-6789",
            email = "jane.doe@example.com",
            diagnosis = "healthy",
            internal_notes = "Follow-up scheduled for next week",
        });
    }

    [HttpGet("logged")]
    [PreEnforce(Action = "readLogged", Resource = "logged")]
    public IActionResult GetLogged()
    {
        return Ok(new
        {
            message = "This response was logged by a policy obligation",
            data = new { patientId = "P-001", status = "active" },
        });
    }

    [HttpGet("audited")]
    [PreEnforce(Action = "readAudited", Resource = "audited")]
    public IActionResult GetAudited()
    {
        return Ok(new
        {
            message = "This response was recorded in the audit trail",
            record = new { id = "MR-42", type = "blood-work", result = "normal" },
        });
    }

    [HttpGet("audit-log")]
    public IActionResult GetAuditLog()
    {
        return Ok(_auditTrailHandler.GetAuditLog());
    }

    [HttpGet("redacted")]
    [PreEnforce(Action = "readRedacted", Resource = "redacted")]
    public IActionResult GetRedacted()
    {
        return Ok(new
        {
            name = "John Smith",
            ssn = "987-65-4321",
            creditCard = "4111-1111-1111-1111",
            email = "john@example.com",
            balance = 1500.0,
        });
    }

    [HttpGet("documents")]
    [PreEnforce(Action = "readDocuments", Resource = "documents")]
    public IActionResult GetDocuments()
    {
        return Ok(PatientData.Documents.Select(d => new
        {
            d.Id,
            d.Title,
            d.Classification,
        }));
    }

    [HttpGet("timestamped")]
    [PreEnforce(Action = "readTimestamped", Resource = "timestamped")]
    public IActionResult GetTimestamped()
    {
        var policyTimestamp = HttpContext.Items["policyTimestamp"] as string ?? "not injected";
        return Ok(new
        {
            message = "This response includes a policy-injected timestamp",
            policyTimestamp,
            data = new { sensor = "temp-01", value = 22.5 },
        });
    }

    [HttpGet("error-demo")]
    [PreEnforce(Action = "readErrorDemo", Resource = "errorDemo")]
    public IActionResult GetErrorDemo()
    {
        throw new InvalidOperationException("Simulated backend failure");
    }

    [HttpGet("resource-replaced")]
    [PreEnforce(Action = "readReplaced", Resource = "replaced")]
    public IActionResult GetResourceReplaced()
    {
        return Ok(new
        {
            message = "You should NOT see this -- the PDP replaces this resource",
            originalData = true,
        });
    }

    [HttpGet("advised")]
    [PreEnforce(Action = "readAdvised", Resource = "advised")]
    public IActionResult GetAdvised()
    {
        return Ok(new
        {
            message = "Access granted despite unhandled advice",
            data = new { category = "medical", status = "reviewed" },
        });
    }

    [HttpGet("record/{id}")]
    [PostEnforce(Action = "readRecord")]
    public IActionResult GetRecord(string id)
    {
        return Ok(new
        {
            id,
            value = "sensitive-data",
            classification = "confidential",
        });
    }

    [HttpGet("unhandled")]
    [PreEnforce(Action = "readSecret", Resource = "secret")]
    public IActionResult GetUnhandled()
    {
        return Ok(new { data = "you should not see this" });
    }
}
