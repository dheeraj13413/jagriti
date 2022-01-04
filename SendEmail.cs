using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Azure.Storage.Blobs;
using System.Net.Mime;


namespace CmsInteroperability.Utilities
{
    class SendEmail
    {

        [FunctionName("SendEmail")]
        public static async Task<IActionResult> Run(
     [HttpTrigger(AuthorizationLevel.Anonymous, "post", "get", Route = null)] HttpRequest req,
     ILogger log)
        {
            try
            {
                const string Audit_Container = "cont-audit";

                string EmailAddress = req.Query["EmailAddress"];
                string EmailSubject = req.Query["EmailSubject"];
                string EmailBody = req.Query["EmailBody"];
                string AuditBlobName0 = req.Query["AuditFileName0"];
                string AuditBlobName1 = req.Query["AuditFileName1"];

                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                dynamic data = JsonConvert.DeserializeObject(requestBody);
                EmailAddress = EmailAddress ?? data?.EmailAddress;
                EmailSubject = EmailSubject ?? data?.EmailSubject;
                EmailBody = EmailBody ?? data?.EmailBody;
                AuditBlobName0 = AuditBlobName0 ?? data?.AuditFileName0;
                AuditBlobName1 = AuditBlobName1 ?? data?.AuditFileName1;

                var username = Environment.GetEnvironmentVariable("SendEmailUsername");
                var password = Environment.GetEnvironmentVariable("SendEmailPassword");
                var logStroageAccountConnection = Environment.GetEnvironmentVariable("SaLogStrorageAccount");

                SmtpClient smtpClient = new SmtpClient();
                NetworkCredential basicCredential = new NetworkCredential(username, password);
                MailMessage message = new MailMessage();

                MailAddress fromAddress = new MailAddress(username);

                smtpClient.Host = "smtp.office365.com";
                smtpClient.UseDefaultCredentials = false;
                smtpClient.Credentials = basicCredential;
                smtpClient.EnableSsl = true;
                smtpClient.Port = 25;

                message.From = fromAddress;
                //message.Subject = "Verification Mail from BCBSRI";
                message.Subject = EmailSubject;
                // Set IsBodyHtml to true means you can send HTML email.
                message.IsBodyHtml = true;
                //message.Body = $"< h3 > Hello </ h3 > ";
                message.Body = EmailBody;
                //message.To.Add("Vijayakumar.Shanmugasundaram@bcbsri.org");

                if (!string.IsNullOrWhiteSpace(AuditBlobName0))
                {
                    var attachment0 = FileAttach(logStroageAccountConnection, Audit_Container, AuditBlobName0);
                    var attachment1 = FileAttach(logStroageAccountConnection, Audit_Container, AuditBlobName1);

                    if (attachment0 == null)
                    {
                        message.Body = message.Body + "........" + "attachment failure because of file not found";
                    }
                    else
                    {
                        message.Attachments.Add(attachment0);
                        if (attachment1 != null)
                        {
                            message.Attachments.Add(attachment1);
                        }
                    }
                }

                message.To.Add(EmailAddress);
                smtpClient.Send(message);
                //return true;

                string responseMessage = string.IsNullOrEmpty(EmailAddress)
                ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
                : $"Email has sent succesfully to {EmailAddress}. This HTTP triggered function executed successfully.";

                return new OkObjectResult(responseMessage);
            }
            catch (Exception ex)
            {
                log.LogInformation("Error in sending Email");
                log.LogError($"error in send email function is#{ex.Message}");
                Console.WriteLine(ex.Message);
                return new OkObjectResult(false);
            }
        }

        private static Attachment FileAttach(string stroageConnection, string Container, string fileName)
        {
            var serviceClient = new BlobServiceClient(stroageConnection);
            var containerClient = serviceClient.GetBlobContainerClient(Container);
            var blobClient = containerClient.GetBlobClient(fileName);

            if (!blobClient.Exists())
            {
                return null;
            }
            var stream = new MemoryStream();
            var x = blobClient.DownloadTo(stream);
            stream.Seek(0, SeekOrigin.Begin);
            var content = new ContentType(MediaTypeNames.Text.Plain);
            var attachment = new Attachment(stream, fileName, MediaTypeNames.Text.Plain);

            return attachment;
        }
    }
}
