using Microsoft.AspNetCore.Mvc;
using Sapl.Demo.Services;

namespace Sapl.Demo.Controllers;

[ApiController]
[Route("api/services")]
public sealed class ServicesController : ControllerBase
{
    private readonly PatientService _patientService;

    public ServicesController(PatientService patientService)
    {
        _patientService = patientService;
    }

    [HttpGet("patients")]
    public async Task<IActionResult> ListPatients()
    {
        var result = await _patientService.ListPatients(HttpContext.RequestAborted);
        return Ok(result);
    }

    [HttpGet("patients/find")]
    public async Task<IActionResult> FindPatient([FromQuery] string name)
    {
        var result = await _patientService.FindPatient(name, HttpContext.RequestAborted);
        return Ok(result);
    }

    [HttpGet("patients/search")]
    public async Task<IActionResult> SearchPatients([FromQuery] string q)
    {
        var result = await _patientService.SearchPatients(q, HttpContext.RequestAborted);
        return Ok(result);
    }

    [HttpGet("patients/{id}/summary")]
    public async Task<IActionResult> GetPatientSummary(string id)
    {
        var result = await _patientService.GetPatientSummary(id, HttpContext.RequestAborted);
        if (result is null)
            return NotFound();
        return Ok(result);
    }

    [HttpGet("patients/{id}")]
    public async Task<IActionResult> GetPatientDetail(string id)
    {
        var result = await _patientService.GetPatientDetail(id, HttpContext.RequestAborted);
        if (result is null)
            return NotFound();
        return Ok(result);
    }

    [HttpPost("transfer")]
    public async Task<IActionResult> Transfer([FromQuery] double amount)
    {
        var result = await _patientService.Transfer(amount, HttpContext.RequestAborted);
        return Ok(result);
    }
}
