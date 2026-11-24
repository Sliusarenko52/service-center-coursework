using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using ServiceCenter.Web.Data;
using ServiceCenter.Web.Models;

namespace ServiceCenter.Web.Services;

/// <summary>
/// Сервіс автентифікації, реєстрації та отримання довідника користувачів за ролями.
/// </summary>
public sealed class AuthService
{
    private readonly SqliteConnectionFactory _db;
    private readonly PasswordHasher<UserRecord> _hasher = new();

    public AuthService(SqliteConnectionFactory db)
    {
        _db = db;
    }

    public UserDto Authenticate(string email, string password)
    {
        var normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalizedEmail) || string.IsNullOrEmpty(password))
        {
            throw new ValidationAppException("Введіть електронну пошту та пароль.");
        }

        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT id, email, full_name, phone, role, password_hash
            FROM users
            WHERE lower(email) = @email
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("@email", normalizedEmail);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            throw new ValidationAppException("Невірна електронна пошта або пароль.");
        }

        var record = new UserRecord
        {
            Id = reader.GetInt64(0),
            Email = reader.GetString(1),
            FullName = reader.GetString(2),
            Phone = reader.GetString(3),
            Role = reader.GetString(4),
            PasswordHash = reader.GetString(5)
        };

        var result = _hasher.VerifyHashedPassword(record, record.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
        {
            throw new ValidationAppException("Невірна електронна пошта або пароль.");
        }

        return new UserDto(record.Id, record.Email, record.FullName, record.Phone, record.Role);
    }

    public UserDto RegisterClient(RegisterRequest req)
    {
        var email = (req.Email ?? string.Empty).Trim().ToLowerInvariant();
        var fullName = (req.FullName ?? string.Empty).Trim();
        var phone = (req.Phone ?? string.Empty).Trim();
        var password = req.Password ?? string.Empty;

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrEmpty(email) || !email.Contains('@') || email.Length > 120)
            errors["email"] = ["Вкажіть коректну електронну пошту (до 120 символів)."];
        if (fullName.Length < 3 || fullName.Length > 120)
            errors["fullName"] = ["ПІБ має містити від 3 до 120 символів."];
        if (phone.Length < 7 || phone.Length > 25)
            errors["phone"] = ["Вкажіть коректний номер телефону (7–25 символів)."];
        if (password.Length < 15 || password.Length > 128)
            errors["password"] = ["Пароль має містити від 15 до 128 символів (згідно з NIST SP 800-63B-4)."];

        if (errors.Count > 0)
            throw new ValidationAppException("Помилка валідації реєстраційних даних.", errors);

        using var conn = _db.CreateConnection();
        using var check = conn.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM users WHERE lower(email) = @email;";
        check.Parameters.AddWithValue("@email", email);
        if (Convert.ToInt64(check.ExecuteScalar()) > 0)
        {
            throw new ConflictAppException("Користувач із такою електронною поштою вже зареєстрований.", "EMAIL_EXISTS");
        }

        var user = new UserRecord
        {
            Email = email,
            FullName = fullName,
            Phone = phone,
            Role = UserRoles.Client
        };
        var hash = _hasher.HashPassword(user, password);

        using var insert = conn.CreateCommand();
        insert.CommandText = """
            INSERT INTO users (email, full_name, phone, role, password_hash, created_at)
            VALUES (@email, @name, @phone, @role, @hash, @created);
            SELECT last_insert_rowid();
            """;
        insert.Parameters.AddWithValue("@email", email);
        insert.Parameters.AddWithValue("@name", fullName);
        insert.Parameters.AddWithValue("@phone", phone);
        insert.Parameters.AddWithValue("@role", UserRoles.Client);
        insert.Parameters.AddWithValue("@hash", hash);
        insert.Parameters.AddWithValue("@created", DateTime.UtcNow.ToString("O"));

        var newId = Convert.ToInt64(insert.ExecuteScalar());
        return new UserDto(newId, email, fullName, phone, UserRoles.Client);
    }

    public UserDto? GetUserById(long id)
    {
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, email, full_name, phone, role FROM users WHERE id = @id LIMIT 1;";
        cmd.Parameters.AddWithValue("@id", id);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return null;
        return new UserDto(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4));
    }

    public IReadOnlyList<UserDto> GetUsersByRole(string role)
    {
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, email, full_name, phone, role FROM users WHERE role = @role ORDER BY full_name;";
        cmd.Parameters.AddWithValue("@role", role);
        var list = new List<UserDto>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new UserDto(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4)));
        }
        return list;
    }
}
