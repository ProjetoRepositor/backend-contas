using Amazon.DynamoDBv2.DataModel;

namespace App.Models;

[DynamoDBTable("TCC_Conta")]
public class Conta
{
    [DynamoDBHashKey]
    public string Email { get; set; } = string.Empty;
    public string? SenhaUsoUnico { get; set; } = string.Empty;
}