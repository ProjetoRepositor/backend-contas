using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace App.Services;

public class Metodos
{
    public static string HashSenha(string senha)
    {
        byte[] salt = { 5, 10, 15, 20, 25, 30, 35 };
        
        string hashed = Convert.ToBase64String(KeyDerivation.Pbkdf2(
            password: senha,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: 100000,
            numBytesRequested: 256 / 8));

        return hashed;
    }

    public static bool ValidaEmail(string email)
    {
        const string pattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";

        const string dominioPattern = @".*\.{2,}.*";
        
        var matchPattern = Regex.IsMatch(email, pattern);

        var dominio = pattern.Split("@")[1];

        var dominioValido = !Regex.IsMatch(dominio, dominioPattern);

        return matchPattern && dominioValido;
    }
    
    public static bool ValidaSenha(string password)
    {
        const string pattern = @"^(?=.*[A-Z])(?=.*[a-z])(?=.*\d)(?=.*[!@#$%^&*()_+])[A-Za-z\d!@#$%^&*()_+]{8,}$";
        
        return Regex.IsMatch(password, pattern);
    }

    public static bool ValidaNome(string password)
    {
        var tamanhoMinimo = 3;
        var contemSobrenome = password.Contains(' ');
        var possuiTamanhoMinimo = password.Length > tamanhoMinimo;
        return contemSobrenome && possuiTamanhoMinimo;
    }

    public static bool ValidaCpf(string cpf)
    {
        if (cpf.Length != 11)
            return false;

        bool todosOsDigitosIguais = true;
        for (int posicao = 1; posicao < 11; posicao++)
        {
            if (cpf[posicao] != cpf[0])
            {
                todosOsDigitosIguais = false;
            }
        }

        if (todosOsDigitosIguais)
            return false;
        
        
        // Calcula o primeiro dígito verificador
        int somatorio = 0;
        
        for (int posicao = 0; posicao < 9; posicao++)
        {
            somatorio += Convert.ToInt16(cpf[posicao].ToString()) * (10 - posicao);
        }
        
        int resto = somatorio % 11;
        
        int digito1 = (resto < 2) ? 0 : 11 - resto;
        
        

        // Calcula o segundo dígito verificador
        somatorio = 0;
        for (int posicao = 0; posicao < 10; posicao++)
        {
            somatorio +=Convert.ToInt16(cpf[posicao].ToString()) * (11 - posicao);
        }

        resto = somatorio % 11;
        
        int digito2 = (resto < 2) ? 0 : 11 - resto;
        

        // Verifica se os dígitos verificadores estão corretos
        if (Convert.ToInt16(cpf[9].ToString()) != digito1 || Convert.ToInt16(cpf[10].ToString()) != digito2)
        {
            return false;
        }

        return true;
    }

    public static bool ValidaIdade(DateOnly dataNascimento)
    {
        return ValidaIdade(dataNascimento, out _);
    }

    public static bool ValidaIdade(DateOnly dataNascimento, out int idade)
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        
        idade = hoje.Year - dataNascimento.Year;

        var naoPassouOMesDoAniversario = hoje.Month < dataNascimento.Month;

        var naoPassouODiaDoAniversario = hoje.Month == dataNascimento.Month && hoje.Day < dataNascimento.Day;

        if (naoPassouODiaDoAniversario || naoPassouOMesDoAniversario ) idade--;

        return idade >= 18;
    }
}