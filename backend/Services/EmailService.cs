using System.Globalization;
using System.Net;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using PadelBooking.Api.Options;

namespace PadelBooking.Api.Services;

public sealed class EmailService : IEmailService
{
    private static readonly CultureInfo SerbianCulture =
        CultureInfo.GetCultureInfo("sr-Latn-RS");

    private readonly EmailOptions _options;

    public EmailService(IOptions<EmailOptions> options)
    {
        _options = options.Value;
    }

    public async Task SendReservationConfirmationAsync(
        ReservationConfirmationEmail confirmation,
        CancellationToken cancellationToken = default)
    {
        await SendAsync(
            confirmation.RecipientEmail,
            $"Potvrda rezervacije #{confirmation.ReservationId}",
            BuildConfirmationHtmlBody(confirmation),
            BuildConfirmationTextBody(confirmation),
            cancellationToken);
    }

    public async Task SendReservationCancellationAsync(
        ReservationCancellationEmail cancellation,
        CancellationToken cancellationToken = default)
    {
        await SendAsync(
            cancellation.RecipientEmail,
            $"Rezervacija otkazana #{cancellation.ReservationId}",
            BuildCancellationHtmlBody(cancellation),
            BuildCancellationTextBody(cancellation),
            cancellationToken);
    }

    public async Task SendReservationReminderAsync(
        ReservationReminderEmail reminder,
        CancellationToken cancellationToken = default)
    {
        await SendAsync(
            reminder.RecipientEmail,
            $"Podsetnik za rezervaciju #{reminder.ReservationId}",
            BuildReminderHtmlBody(reminder),
            BuildReminderTextBody(reminder),
            cancellationToken);
    }

    private async Task SendAsync(
        string recipientEmail,
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken cancellationToken)
    {
        EnsureConfigurationIsValid();

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromEmail));
        message.To.Add(MailboxAddress.Parse(recipientEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder
        {
            HtmlBody = htmlBody,
            TextBody = textBody
        }.ToMessageBody();

        using var client = new SmtpClient();
        var socketOptions = _options.UseSsl
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTlsWhenAvailable;

        await client.ConnectAsync(
            _options.SmtpHost,
            _options.SmtpPort,
            socketOptions,
            cancellationToken);

        try
        {
            if (!string.IsNullOrWhiteSpace(_options.Username))
            {
                await client.AuthenticateAsync(
                    _options.Username,
                    _options.Password,
                    cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true, CancellationToken.None);
            }
        }
    }

    private void EnsureConfigurationIsValid()
    {
        if (string.IsNullOrWhiteSpace(_options.SmtpHost) ||
            _options.SmtpPort is <= 0 or > 65535 ||
            string.IsNullOrWhiteSpace(_options.FromEmail))
        {
            throw new InvalidOperationException(
                "SMTP email konfiguracija nije kompletna.");
        }

        if (!string.IsNullOrWhiteSpace(_options.Username) &&
            string.IsNullOrWhiteSpace(_options.Password))
        {
            throw new InvalidOperationException(
                "SMTP credentials nisu kompletni.");
        }
    }

    private static string BuildConfirmationHtmlBody(ReservationConfirmationEmail data)
    {
        var name = Encode(data.UserFirstName);
        var court = Encode(data.CourtName);
        var location = Encode(data.CourtLocation);
        var date = data.StartTime.ToString("dd.MM.yyyy.", SerbianCulture);
        var time = $"{data.StartTime:HH:mm} &ndash; {data.EndTime:HH:mm}";
        var price = Encode(FormatPrice(data.TotalPrice));

        return $$"""
            <!doctype html>
            <html lang="sr">
            <body style="margin:0;padding:0;background:#F6F4EE;color:#17201B;font-family:Arial,sans-serif;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#F6F4EE;padding:32px 12px;">
                <tr>
                  <td align="center">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:600px;background:#FFFFFF;border:1px solid #E2E5DF;border-radius:12px;overflow:hidden;">
                      <tr>
                        <td style="background:#18392B;padding:28px 32px;color:#FFFFFF;">
                          <div style="font-size:13px;letter-spacing:1.5px;color:#FF7547;font-weight:bold;">PADELBOOKING</div>
                          <h1 style="margin:10px 0 0;font-size:26px;line-height:1.25;">Rezervacija potvrđena</h1>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:32px;">
                          <p style="margin:0 0 12px;font-size:17px;">Zdravo {{name}},</p>
                          <p style="margin:0 0 24px;line-height:1.6;color:#68736C;">Tvoja rezervacija je uspešno potvrđena.</p>
                          <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#F6F4EE;border-left:4px solid #FF7547;border-radius:8px;">
                            <tr><td style="padding:20px 22px;line-height:1.8;">
                              <strong>Teren:</strong> {{court}}<br>
                              <strong>Lokacija:</strong> {{location}}<br>
                              <strong>Datum:</strong> {{date}}<br>
                              <strong>Vreme:</strong> {{time}}<br>
                              <strong>Cena:</strong> {{price}} RSD<br>
                              <strong>Broj rezervacije:</strong> #{{data.ReservationId}}
                            </td></tr>
                          </table>
                          <p style="margin:26px 0 4px;line-height:1.6;">Vidimo se na terenu!</p>
                          <p style="margin:0;color:#2F7657;font-weight:bold;">PadelBooking</p>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    private static string BuildConfirmationTextBody(ReservationConfirmationEmail data) => $$"""
        Rezervacija potvrđena

        Zdravo {{data.UserFirstName}},

        Tvoja rezervacija je uspešno potvrđena.

        Teren: {{data.CourtName}}
        Lokacija: {{data.CourtLocation}}
        Datum: {{data.StartTime.ToString("dd.MM.yyyy.", SerbianCulture)}}
        Vreme: {{data.StartTime:HH:mm}} – {{data.EndTime:HH:mm}}
        Cena: {{FormatPrice(data.TotalPrice)}} RSD
        Broj rezervacije: #{{data.ReservationId}}

        Vidimo se na terenu!
        PadelBooking
        """;

    private static string BuildCancellationHtmlBody(ReservationCancellationEmail data)
    {
        var name = Encode(data.UserFirstName);
        var court = Encode(data.CourtName);
        var location = Encode(data.CourtLocation);
        var date = data.StartTime.ToString("dd.MM.yyyy.", SerbianCulture);
        var time = $"{data.StartTime:HH:mm} &ndash; {data.EndTime:HH:mm}";
        var price = Encode(FormatPrice(data.TotalPrice));

        return $$"""
            <!doctype html>
            <html lang="sr">
            <body style="margin:0;padding:0;background:#F6F4EE;color:#17201B;font-family:Arial,sans-serif;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#F6F4EE;padding:32px 12px;">
                <tr>
                  <td align="center">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:600px;background:#FFFFFF;border:1px solid #E2E5DF;border-radius:12px;overflow:hidden;">
                      <tr>
                        <td style="background:#18392B;padding:28px 32px;color:#FFFFFF;">
                          <div style="font-size:13px;letter-spacing:1.5px;color:#FF7547;font-weight:bold;">PADELBOOKING</div>
                          <h1 style="margin:10px 0 0;font-size:26px;line-height:1.25;">Rezervacija otkazana</h1>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:32px;">
                          <p style="margin:0 0 12px;font-size:17px;">Zdravo {{name}},</p>
                          <p style="margin:0 0 24px;line-height:1.6;color:#68736C;">Tvoja rezervacija je uspešno otkazana.</p>
                          <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#F6F4EE;border-left:4px solid #2F7657;border-radius:8px;">
                            <tr><td style="padding:20px 22px;line-height:1.8;">
                              <strong>Teren:</strong> {{court}}<br>
                              <strong>Lokacija:</strong> {{location}}<br>
                              <strong>Datum:</strong> {{date}}<br>
                              <strong>Vreme:</strong> {{time}}<br>
                              <strong>Cena:</strong> {{price}} RSD<br>
                              <strong>Broj rezervacije:</strong> #{{data.ReservationId}}
                            </td></tr>
                          </table>
                          <p style="margin:26px 0 4px;line-height:1.6;color:#68736C;">Termin je ponovo dostupan drugim igračima.</p>
                          <p style="margin:0;color:#2F7657;font-weight:bold;">PadelBooking</p>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    private static string BuildCancellationTextBody(ReservationCancellationEmail data) => $$"""
        Rezervacija otkazana

        Zdravo {{data.UserFirstName}},

        Tvoja rezervacija je uspešno otkazana.

        Teren: {{data.CourtName}}
        Lokacija: {{data.CourtLocation}}
        Datum: {{data.StartTime.ToString("dd.MM.yyyy.", SerbianCulture)}}
        Vreme: {{data.StartTime:HH:mm}} – {{data.EndTime:HH:mm}}
        Cena: {{FormatPrice(data.TotalPrice)}} RSD
        Broj rezervacije: #{{data.ReservationId}}

        Termin je ponovo dostupan drugim igračima.

        PadelBooking
        """;

    private static string BuildReminderHtmlBody(ReservationReminderEmail data)
    {
        var name = Encode(data.UserFirstName);
        var court = Encode(data.CourtName);
        var location = Encode(data.CourtLocation);
        var date = data.StartTime.ToString("dd.MM.yyyy.", SerbianCulture);
        var time = $"{data.StartTime:HH:mm} &ndash; {data.EndTime:HH:mm}";
        var price = Encode(FormatPrice(data.TotalPrice));

        return $$"""
            <!doctype html>
            <html lang="sr">
            <body style="margin:0;padding:0;background:#F6F4EE;color:#17201B;font-family:Arial,sans-serif;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#F6F4EE;padding:32px 12px;">
                <tr>
                  <td align="center">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:600px;background:#FFFFFF;border:1px solid #E2E5DF;border-radius:12px;overflow:hidden;">
                      <tr>
                        <td style="background:#18392B;padding:28px 32px;color:#FFFFFF;">
                          <div style="font-size:13px;letter-spacing:1.5px;color:#FF7547;font-weight:bold;">PADELBOOKING</div>
                          <h1 style="margin:10px 0 0;font-size:26px;line-height:1.25;">Podsetnik za rezervaciju</h1>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:32px;">
                          <p style="margin:0 0 12px;font-size:17px;">Zdravo {{name}},</p>
                          <p style="margin:0 0 24px;line-height:1.6;color:#68736C;">Podsećamo te da uskoro imaš rezervisan padel teren.</p>
                          <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#F6F4EE;border-left:4px solid #FF7547;border-radius:8px;">
                            <tr><td style="padding:20px 22px;line-height:1.8;">
                              <strong>Teren:</strong> {{court}}<br>
                              <strong>Lokacija:</strong> {{location}}<br>
                              <strong>Datum:</strong> {{date}}<br>
                              <strong>Vreme:</strong> {{time}}<br>
                              <strong>Cena:</strong> {{price}} RSD<br>
                              <strong>Broj rezervacije:</strong> #{{data.ReservationId}}
                            </td></tr>
                          </table>
                          <p style="margin:26px 0 4px;line-height:1.6;color:#2F7657;font-weight:bold;">Tvoj termin počinje za manje od 3 sata.</p>
                          <p style="margin:18px 0 4px;line-height:1.6;">Vidimo se na terenu!</p>
                          <p style="margin:0;color:#2F7657;font-weight:bold;">PadelBooking</p>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    private static string BuildReminderTextBody(ReservationReminderEmail data) => $$"""
        Podsetnik za rezervaciju

        Zdravo {{data.UserFirstName}},

        Podsećamo te da uskoro imaš rezervisan padel teren.

        Teren: {{data.CourtName}}
        Lokacija: {{data.CourtLocation}}
        Datum: {{data.StartTime.ToString("dd.MM.yyyy.", SerbianCulture)}}
        Vreme: {{data.StartTime:HH:mm}} – {{data.EndTime:HH:mm}}
        Cena: {{FormatPrice(data.TotalPrice)}} RSD
        Broj rezervacije: #{{data.ReservationId}}

        Tvoj termin počinje za manje od 3 sata.

        Vidimo se na terenu!
        PadelBooking
        """;

    private static string FormatPrice(decimal price)
    {
        var formatted = price.ToString("N2", SerbianCulture);
        return formatted.EndsWith(",00", StringComparison.Ordinal)
            ? formatted[..^3]
            : formatted;
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);
}
