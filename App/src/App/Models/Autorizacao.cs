using Amazon.DynamoDBv2.DataModel;

namespace App.Models;

[DynamoDBTable("TCC_Autorizacao")]
public class Autorizacao
{
    [DynamoDBHashKey] public string Token { get; set; } = string.Empty;
    public string Email { get; set; } = String.Empty;
}