public sealed record ImportError(
    int LineNumber,
    string RawLine,
    string Message);