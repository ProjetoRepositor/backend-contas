namespace App.Models;

public class LoginRequest
{
    public string Email { get; set; } = String.Empty;
    public string Senha { get; set; } = String.Empty;
}