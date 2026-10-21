using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SystemAll.Data;

namespace SystemAll.Services;

public class PasswordResetService
{
    private readonly AppDbContext _db;
    private readonly EmailService _email;

    public PasswordResetService(AppDbContext db, EmailService email)
    {
        _db = db;
        _email = email;
    }

    // คืน (success, email ที่ mask แล้ว, errorMsg)
    public async Task<(bool, string, string)> RequestOtpAsync(string userName)
    {
        var user = await _db.SysUserMasters
            .FirstOrDefaultAsync(u => u.UserName == userName);

        if (user == null)
            return (false, "", "ไม่พบ Username นี้ในระบบ");

        if (string.IsNullOrWhiteSpace(user.Email))
            return (false, "", "บัญชีนี้ไม่มีอีเมลในระบบ กรุณาติดต่อ ENG4 (5504)");

        // สร้าง OTP 6 หลัก
        var otp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();

        // ปิด OTP เก่าของ user นี้
        var oldOtps = await _db.PasswordResetOtps
            .Where(o => o.UserName == userName && !o.IsUsed)
            .ToListAsync();
        foreach (var o in oldOtps) o.IsUsed = true;

        _db.PasswordResetOtps.Add(new Models.PasswordResetOtp
        {
            UserName = userName,
            OtpCode = otp,
            Email = user.Email,
            CreatedAt = DateTime.Now,
            ExpiresAt = DateTime.Now.AddMinutes(5),
            IsUsed = false
        });
        await _db.SaveChangesAsync();

        var fullName = $"{user.FName} {user.LName}".Trim();
        var sent = await _email.SendOtpAsync(user.Email, otp, fullName);

        if (!sent)
            return (false, "", "ไม่สามารถส่งอีเมลได้ กรุณาลองใหม่อีกครั้ง");

        return (true, MaskEmail(user.Email), "");
    }

    public async Task<bool> VerifyOtpAsync(string userName, string otp)
    {
        var record = await _db.PasswordResetOtps
            .Where(o => o.UserName == userName && o.OtpCode == otp
                     && !o.IsUsed && o.ExpiresAt > DateTime.Now)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        return record != null;
    }

    public async Task<bool> ResetPasswordAsync(string userName, string otp, string newPassword)
    {
        var record = await _db.PasswordResetOtps
            .Where(o => o.UserName == userName && o.OtpCode == otp
                     && !o.IsUsed && o.ExpiresAt > DateTime.Now)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync();

        if (record == null) return false;

        var user = await _db.SysUserMasters
            .FirstOrDefaultAsync(u => u.UserName == userName);
        if (user == null) return false;

        // update password ลง column user_pass
        user.UserPass = newPassword;  // ถ้าระบบเก็บ plain text
        record.IsUsed = true;

        await _db.SaveChangesAsync();
        return true;
    }

    private static string MaskEmail(string email)
    {
        var parts = email.Split('@');
        if (parts.Length != 2) return email;
        var name = parts[0];
        var masked = name.Length <= 2
            ? name[0] + "***"
            : name[..2] + new string('*', Math.Max(name.Length - 2, 3));
        return $"{masked}@{parts[1]}";
    }


    // --- Reset Password
    public async Task<(bool, string)> ChangePasswordAsync(
    string userName, string currentPassword, string newPassword)
    {
        var user = await _db.SysUserMasters
            .FirstOrDefaultAsync(u => u.UserName == userName);

        if (user == null)
            return (false, "ไม่พบ Username นี้ในระบบ");

        // ตรวจรหัสปัจจุบัน — รองรับทั้ง plain และ MD5
        if (!VerifyPassword(currentPassword, user.UserPass))
            return (false, "รหัสผ่านปัจจุบันไม่ถูกต้อง");

        // บันทึกรหัสใหม่ (plain text ตาม format เดิม)
        user.UserPass = newPassword;
        await _db.SaveChangesAsync();

        return (true, "");
    }

    private static bool VerifyPassword(string input, string stored)
    {
        if (input == stored) return true;
        using var md5 = System.Security.Cryptography.MD5.Create();
        var hash = Convert.ToHexString(
            md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input)));
        return string.Equals(hash, stored, StringComparison.OrdinalIgnoreCase);
    }
}

