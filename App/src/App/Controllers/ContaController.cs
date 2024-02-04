using System.Security.Cryptography;
using System.Text;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
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
        
        static string HashSenha(string input)
        {
            using SHA256 sha256 = SHA256.Create();
            
            // Converte a string para bytes
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);

            // Calcula o hash SHA-256
            byte[] hashBytes = sha256.ComputeHash(inputBytes);

            // Converte o hash para uma string hexadecimal
            StringBuilder stringBuilder = new StringBuilder();
            for (int i = 0; i < hashBytes.Length; i++)
            {
                stringBuilder.Append(hashBytes[i].ToString("x2"));
            }

            return stringBuilder.ToString();
        } 

        [HttpPost("CadastrarDispositivo")]
        public async Task<IActionResult> CadstrarDispositivo(DeviceRequest request)
        {
            await _context.SaveAsync(new Autorizacao
            {
                Email = request.UserEmail,
                Token = request.DeviceId,
            });
            
            return Created("", request);
        }

        [HttpPost("Solicitar", Name = "CriarConta")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> SolicitaSenha([FromBody] EmailRequest request)
        {
            var tamanhoSenha = 6;
            var timestamp = DateTime.UtcNow.Ticks.ToString();
            var senhaUsoUnico = timestamp.Substring(timestamp.Length - 3 - tamanhoSenha, tamanhoSenha);

            var snsConfig = new AmazonSimpleNotificationServiceConfig
            {
                RegionEndpoint = RegionEndpoint.SAEast1
            };

            using var snsClient = new AmazonSimpleNotificationServiceClient(snsConfig);

            // Replace all non-letter characters in the email address with "_"
            var topicName = new string(request.Email.Select(c => char.IsLetter(c) ? c : '_').ToArray());

            // Check if the topic already exists
            var existingTopics = await snsClient.ListTopicsAsync();
            var existingTopic = existingTopics.Topics.FirstOrDefault(t => t.TopicArn.EndsWith($":{topicName}"));

            if (existingTopic == null)
            {
                // Create an SNS topic with the modified email as the name
                var createTopicRequest = new CreateTopicRequest
                {
                    Name = topicName
                };

                var createTopicResponse = await snsClient.CreateTopicAsync(createTopicRequest);
                existingTopic = new Topic { TopicArn = createTopicResponse.TopicArn };
            }

            // Check if the user is already subscribed to the topic
            var listSubscriptionsRequest = new ListSubscriptionsByTopicRequest
            {
                TopicArn = existingTopic.TopicArn
            };

            var existingSubscription = (await snsClient.ListSubscriptionsByTopicAsync(listSubscriptionsRequest))
                .Subscriptions.FirstOrDefault(s => s.Endpoint == request.Email);

            if (existingSubscription == null)
            {
                // Subscribe the user to the topic
                var subscribeRequest = new SubscribeRequest
                {
                    Protocol = "email",
                    Endpoint = request.Email,
                    TopicArn = existingTopic.TopicArn
                };

                await snsClient.SubscribeAsync(subscribeRequest);
                
                return Ok(new
                {
                    Message = "Aceite a inscrição em seu e-mail para continuar"
                });
            }

            // Publish a message to the created or existing topic
            var publishRequest = new PublishRequest
            {
                TopicArn = existingTopic.TopicArn,
                Subject = "Senha de uso único",
                Message = $"Sua senha de uso único é {senhaUsoUnico}"
            };

            await _context.SaveAsync(new Conta
            {
                Email = request.Email,
                SenhaUsoUnico = HashSenha(senhaUsoUnico),
            });

            try
            {
                var response = await snsClient.PublishAsync(publishRequest);
                var messageId = response.MessageId;

                return Created("", new
                {
                    Menssage = "Email enviado"
                });
            }
            catch (Exception ex)
            {
                // Handle SNS sending errors
                _logger.LogError($"Erro ao enviar mensagem via SNS: {ex.Message}");
                return StatusCode(StatusCodes.Status500InternalServerError, "Erro ao enviar mensagem via SNS");
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

            if (user != null && user.SenhaUsoUnico != null && user.SenhaUsoUnico == HashSenha(loginRequest.Senha))
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
                .Select(s => s[random.Next(s.Length)]).ToArray()) + userEmail;

            authToken = HashSenha(authToken);
    
            return authToken;
        }

    }
}
