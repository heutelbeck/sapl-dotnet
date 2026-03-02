using Microsoft.AspNetCore.Mvc;
using Sapl.AspNetCore.Attributes;
using Sapl.Demo.Data;

namespace Sapl.Demo.Controllers;

[ApiController]
[Route("api")]
public sealed class PatientController : ControllerBase
{
    [HttpGet("patient/{id}")]
    [PreEnforce(Action = "readPatient", Resource = "patient")]
    public IActionResult GetPatient(string id)
    {
        var patient = PatientData.Patients.FirstOrDefault(p => p.Id == id);
        if (patient is null)
            return NotFound();
        return Ok(new { patient.Name, patient.Ssn, patient.Diagnosis, email = "jane.doe@example.com" });
    }

    [HttpGet("patients")]
    [PostEnforce(Action = "readPatients", Resource = "patients")]
    public IActionResult GetPatients()
    {
        return Ok(PatientData.Patients.Select(p => new
        {
            p.Id,
            p.Name,
            p.Ssn,
            p.Diagnosis,
            p.Classification,
        }));
    }

    [HttpPost("transfer")]
    [PreEnforce(Action = "transfer", Resource = "account")]
    public IActionResult Transfer([FromQuery] double amount)
    {
        return Ok(new { transferred = amount });
    }
}
