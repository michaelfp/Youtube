using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Xrm.Tooling.Connector;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Services.Description;
using System.Linq;
using System.Runtime.InteropServices;

namespace MFP.BackgroundOperations.ConsoleApp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var crmclient = new CrmServiceClient(ConfigurationManager.ConnectionStrings["DataverseConnection"].ConnectionString);

            Console.WriteLine("Pressione Qualquer tecla para iniciar:");
            Console.ReadKey();
            Console.WriteLine("Executando trabalho em segundo plano...");

            var backgroundId = ExecutarTrabalhoSegundoPlano(crmclient, true);

            var statusTrabalho = ConsultarStatusProcesso(crmclient, backgroundId);

            while (statusTrabalho != BackgroundStatus.Succeeded && statusTrabalho != BackgroundStatus.Failed)
            {
                Thread.Sleep(5000);
                statusTrabalho = ConsultarStatusProcesso(crmclient, backgroundId);
            }

            if (statusTrabalho == BackgroundStatus.Succeeded)
            {
                var respostaTrabalho = ObterRetornoProcesso(crmclient, backgroundId);

                respostaTrabalho.ForEach(arg => Console.WriteLine($"Parâmetro: {arg.Key}: \r Valor: {arg.Value}"));
            }

            if (statusTrabalho == BackgroundStatus.Failed)
            {
                ConsultarErro(crmclient, backgroundId);
            }
            Console.ReadLine();
        }

        public static Guid ExecutarTrabalhoSegundoPlano(CrmServiceClient client, bool withCallback = false)
        {

            //Cria o request da action
            var request = new OrganizationRequest("mfp_gerar_nota_fiscal")
            {
                Parameters = {
                    {
                        "Target", new EntityReference("mfp_pedidovenda", new Guid("b3b04ba8-3e3b-ef11-8409-000d3a1ccada"))


                    }
                    
                }

            };

           

            //Cria Request do trabalho em segundo plano
            var requestBackground = new OrganizationRequest("ExecuteBackgroundOperation")
            {
                Parameters = {
                    { "Request",request},
                    {
                        "CallbackUri", "https://webhook.site/0b414875-68ae-4958-bcc2-6dafaefcc812"
                    }

                }
            };

            //Executa o comando
            var response = client.Execute(requestBackground);

            var backgroundId = new Guid(response["BackgroundOperationId"].ToString());


            Console.WriteLine($"BackgroundOperationId: {backgroundId}");

            return backgroundId;
        }

        public static BackgroundStatus ConsultarStatusProcesso(CrmServiceClient client, Guid backgroundId)
        {
            var columnSet = new ColumnSet(
                   "name",
                   "backgroundoperationstatecode",
                   "backgroundoperationstatuscode",
                   "outputparameters",
                   "errorcode",
                   "errormessage");

            try
            {
                // Get the entity with all the required columns
                var backgroundOperation = client.Retrieve("backgroundoperation", backgroundId, columnSet);

                Console.WriteLine($"Name: {backgroundOperation["name"]}");
                Console.WriteLine($"State Code: {backgroundOperation.FormattedValues["backgroundoperationstatecode"]}");
                Console.WriteLine($"Status Code: {backgroundOperation.FormattedValues["backgroundoperationstatuscode"]}");
                Console.WriteLine(Environment.NewLine);

                return (BackgroundStatus)backgroundOperation.GetAttributeValue<OptionSetValue>("backgroundoperationstatuscode").Value;

            }
            // Catch Dataverse errors
            catch (FaultException<OrganizationServiceFault> ex)
            {
                Console.WriteLine($"ErrorCode:{ex.Detail.ErrorCode}");
                Console.WriteLine($"Message:{ex.Detail.Message}");

                return BackgroundStatus.Failed;
            }
            // Catch other errors
            catch (Exception error)
            {
                Console.WriteLine($"Some other error occurred: '{error.Message}'");

                return BackgroundStatus.Failed;
            }
        }


        public static void ConsultarErro(CrmServiceClient client, Guid backgroundId)
        {
            var columnSet = new ColumnSet(
                   "name",
                   "errorcode",
                   "errormessage");

            try
            {
                // Get the entity with all the required columns
                var backgroundOperation = client.Retrieve("backgroundoperation", backgroundId, columnSet);

                Console.WriteLine($"Error Code: {backgroundOperation.GetAttributeValue<string>("errorcode")}");
                Console.WriteLine($"Error Message: {backgroundOperation.GetAttributeValue<string>("errormessage")}");

            }
            // Catch Dataverse errors
            catch (FaultException<OrganizationServiceFault> ex)
            {
                Console.WriteLine($"ErrorCode:{ex.Detail.ErrorCode}");
                Console.WriteLine($"Message:{ex.Detail.Message}");

            }
            // Catch other errors
            catch (Exception error)
            {
                Console.WriteLine($"Some other error occurred: '{error.Message}'");

            }
        }

        public static List<KeyValuePair<string, string>> ObterRetornoProcesso(CrmServiceClient client, Guid backgroundId)
        {
            // List of columns that will help to get status, output and error details if any
            var columnSet = new ColumnSet(
                "name",
                "outputparameters",
                "errorcode"
                );

            try
            {
                // Get the entity with all the required columns
                var backgroundOperation = client.Retrieve("backgroundoperation", backgroundId, columnSet);

                Console.WriteLine($"Output Parameters:");

                // Deserialize the Output Parameters into KeyValuePair<string, string>
                List<KeyValuePair<string, string>> output =
                    System.Text.Json.JsonSerializer
                    .Deserialize<List<KeyValuePair<string, string>>>((string)backgroundOperation["outputparameters"]);

                return output;
            }
            // Catch Dataverse errors
            catch (FaultException<OrganizationServiceFault> ex)
            {
                Console.WriteLine($"ErrorCode:{ex.Detail.ErrorCode}");
                Console.WriteLine($"Message:{ex.Detail.Message}");


            }
            // Catch other errors
            catch (Exception error)
            {
                Console.WriteLine($"Some other error occurred: '{error.Message}'");
            }

            return null;
        }
    }

    public enum BackgroundStatus
    {
        WaitingForResources = 0,
        InProgress = 20,
        Canceling = 22,
        Succeeded = 30,
        Failed = 31,
        Canceled = 32

    }


}
