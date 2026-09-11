namespace PadelBooking.Application.Abstractions.Storage;

public sealed record CourtImageUpload(
    string FileName,
    string ContentType,
    long Length,
    Stream Content);
