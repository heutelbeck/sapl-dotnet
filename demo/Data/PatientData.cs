namespace Sapl.Demo.Data;

public sealed record PatientRecord(
    string Id,
    string Name,
    string Ssn,
    string Diagnosis,
    string Classification);

public sealed record DocumentRecord(
    string Id,
    string Title,
    string Classification);

public static class PatientData
{
    public static readonly PatientRecord[] Patients =
    [
        new("P-001", "Jane Doe", "123-45-6789", "healthy", "INTERNAL"),
        new("P-002", "John Smith", "987-65-4321", "checkup", "CONFIDENTIAL"),
        new("P-003", "Alice Johnson", "555-12-3456", "healthy", "PUBLIC"),
    ];

    public static readonly DocumentRecord[] Documents =
    [
        new("DOC-1", "Company Newsletter", "PUBLIC"),
        new("DOC-2", "Team Standup Notes", "INTERNAL"),
        new("DOC-3", "Patient Records", "CONFIDENTIAL"),
        new("DOC-4", "Encryption Keys", "SECRET"),
    ];
}
