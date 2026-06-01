namespace Sapl.Core.Pep.Constraints;

/// <summary>
/// The lifecycle points at which constraint handlers attach. Decision, Input,
/// Output, and Error carry a value. Cancel, Complete, and Termination do not.
/// </summary>
public enum SignalKind
{
    Decision,
    Input,
    Output,
    Error,
    Cancel,
    Complete,
    Termination,
}
