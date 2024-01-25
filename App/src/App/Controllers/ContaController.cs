using System.Net.Mail;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using App.Models; // Adicione esta linha

namespace App.Controllers
{
    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    [Route("/api/v1/autenticacao/[controller]")]
    public class ContaController : ControllerBase
    {
        private readonly ILogger<ContaController> _logger;
        private DynamoDBContext _context;

        public ContaController(ILogger<ContaController> logger, IAmazonDynamoDB dynamoDb)
        {
            _logger = logger;
            _context = new DynamoDBContext(dynamoDb);
        }

        [HttpPost("Solicitar", Name = "CriarConta")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SolicitaSenha([FromBody] EmailRequest request)
        {
            var tamanhoSenha = 6;
            var timestamp = DateTime.UtcNow.Ticks.ToString();
            var posicaoInicial = timestamp.Length - 3 - tamanhoSenha;
            var senhaUsoUnico = timestamp.Substring(posicaoInicial, tamanhoSenha);

            var sesConfig = new AmazonSimpleEmailServiceConfig
            {
                RegionEndpoint = RegionEndpoint.SAEast1
            };

            using var sesClient = new AmazonSimpleEmailServiceClient(sesConfig);

            var fromAddress = new MailAddress("nao-responda@tcc.caioruiz.com", "Carrinho Inteligente");
            var toAddress = new MailAddress(request.Email);
            var subject = "Senha de uso único";
            var body = $"Sua senha de uso único é {senhaUsoUnico}";

            var sendRequest = new SendEmailRequest
            {
                Source = fromAddress.Address,
                Destination = new Destination { ToAddresses = new List<string> { toAddress.Address } },
                Message = new Message
                {
                    Subject = new Content(subject),
                    Body = new Body
                    {
                        Text = new Content(body)
                    }
                }
            };

            await _context.SaveAsync(new Conta
            {
                Email = request.Email,
                SenhaUsoUnico = senhaUsoUnico,
            });

            try
            {
                var response = await sesClient.SendEmailAsync(sendRequest);
                var messageId = response.MessageId;

                return Ok(messageId);
            }
            catch (Exception ex)
            {
                // Tratar erros de envio de e-mail
                _logger.LogError($"Erro ao enviar e-mail: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, "Erro ao enviar e-mail");
            }
        }
        
        [HttpPost("Login", Name = "Login")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Login([FromBody] LoginRequest loginRequest)
        {
            // Verifique se as credenciais do usuário são válidas (por exemplo, consultando o DynamoDB)
            var user = await _context.LoadAsync<Conta>(loginRequest.Email);

            if (user != null && user.SenhaUsoUnico != null && user.SenhaUsoUnico == loginRequest.Senha)
            {
                await _context.SaveAsync(new Conta
                {
                    Email = loginRequest.Email,
                    SenhaUsoUnico = null,
                });

                var token = GenerateJwtToken(user.Email);
                
                await _context.SaveAsync(new Autorizacao
                {
                    Token = token,
                    Email = loginRequest.Email,
                });
                
                return Ok(new { Token = token });
            }
            return Unauthorized("Credenciais inválidas");
        }

        private string GenerateJwtToken(string userEmail)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
    
            const int tokenLength = 20;
    
            var authToken = new string(Enumerable.Repeat(chars, tokenLength)
                .Select(s => s[random.Next(s.Length)]).ToArray());
    
            return authToken;
        }

    }
}
