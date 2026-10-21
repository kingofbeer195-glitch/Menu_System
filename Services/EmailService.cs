using System.Net;
using System.Net.Mail;

namespace SystemAll.Services;

public class EmailService
{
    private const string SmtpHost = "150.61.249.70";
    private const int SmtpPort = 25;
    private const string FromEmail = "engineer_pde@mail.canon";

    public async Task<bool> SendOtpAsync(string toEmail, string otp, string fullName)
    {
        try
        {
            var body = $@"
                    <div style='font-family:Segoe UI,sans-serif; max-width:480px; margin:auto;
                                border:1px solid #e0e0e0; border-radius:12px; overflow:hidden;'>
                        <div style='background:#1a2a6c; color:#fff; padding:20px; text-align:center;'>
                            <h2 style='margin:0;'>ENG4 System</h2>
                            <p style='margin:4px 0 0; font-size:13px;'>รหัสยืนยันการเปลี่ยนรหัสผ่าน</p>
                        </div>
                        <div style='padding:24px; color:#333;'>
                            <p>เรียน คุณ {fullName},</p>
                            <p>รหัสยืนยันชั่วคราว (OTP) ของคุณคือ:</p>
                            <div style='text-align:center; margin:20px 0;'>
                                <span style='font-size:32px; font-weight:bold; letter-spacing:8px;
                                             color:#1a2a6c; background:#f0f4ff; padding:12px 24px;
                                             border-radius:8px;'>{otp}</span>
                            </div>
                            <p style='color:#d32f2f; font-size:14px;'>
                                ⏱ รหัสนี้จะหมดอายุภายใน 5 นาที</p>
                            <p style='font-size:13px; color:#888;'>
                                หากคุณไม่ได้ร้องขอการเปลี่ยนรหัสผ่าน กรุณาเพิกเฉยต่ออีเมลนี้</p>
                        </div>
                        <div style='background:#f5f5f5; padding:12px; text-align:center;
                                    font-size:12px; color:#999;'>
                            ENG4 Engineering Department · ต่อ 5504
                        </div>
                    </div>";

            using var message = new MailMessage
            {
                From = new MailAddress(FromEmail, "ENG4 System"),
                Subject = "รหัสยืนยันการเปลี่ยนรหัสผ่าน (OTP)",
                Body = body,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            using var client = new SmtpClient(SmtpHost, SmtpPort)
            {
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = true,
                EnableSsl = false
            };

            await client.SendMailAsync(message);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> SendAsync(string toEmail, string subject, string bodyHtml)
    {
        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(FromEmail, "ENG4 System"),
                Subject = subject,
                Body = bodyHtml,
                IsBodyHtml = true
            };
            message.To.Add(toEmail);

            using var client = new SmtpClient(SmtpHost, SmtpPort)
            {
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = true,
                EnableSsl = false
            };

            await client.SendMailAsync(message);
            return true;
        }
        catch
        {
            return false;
        }
    }

}