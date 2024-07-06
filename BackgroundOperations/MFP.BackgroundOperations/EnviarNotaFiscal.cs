using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace MFP.BackgroundOperations
{
    public class EnviarNotaFiscal : IPlugin
    {
        IPluginExecutionContext context;
        IOrganizationServiceFactory serviceFactory;
        IOrganizationService orgService;
        ITracingService tracingService;

        public void Execute(IServiceProvider serviceProvider)
        {
            context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            orgService = serviceFactory.CreateOrganizationService(context.UserId);
            tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));


            if (context.MessageName.Equals("mfp_gerar_nota_fiscal"))
            {
                if (context.InputParameters.Contains("Target"))
                {
                    EntityReference orderParameter = null;

                    context.InputParameters.TryGetValue<EntityReference>("Target", out orderParameter);

                    if (orderParameter != null)
                    {
                        Entity order = orgService.Retrieve(orderParameter.LogicalName, orderParameter.Id, new Microsoft.Xrm.Sdk.Query.ColumnSet(true));

                        if (order != null)
                        {
                            Thread.Sleep(14000);
                            string xmlNota = ObterNota(order["mfp_numeropedido"].ToString());

                            if (!string.IsNullOrEmpty(xmlNota))
                            {
                                order["mfp_nota_fiscal"] = xmlNota;

                                orgService.Update(order);

                                context.OutputParameters["resultado"] = "Nota fiscal gerada com sucesso!";
                            }

                        }
                    }
                }
            }
        }

        public string ObterNota(string numeroPedido)
        {
            HttpClient client = new HttpClient();

            HttpContent content = new StringContent($"{{\"numeroPedido\": \"{numeroPedido}\"}}", Encoding.UTF8, "application/json");

            var responsePost = client.PostAsync($"https://webhook.site/f07a2a3d-6c0c-491b-aa30-850111c59459", content).Result;

            if (responsePost.StatusCode != HttpStatusCode.OK)
            {
                throw new InvalidPluginExecutionException("Erro ao chamar serviço de geração de nota fiscal");
            }

            var jsonString = responsePost.Content.ReadAsStringAsync().Result;

            tracingService.Trace("Response: " + jsonString);

            ResponseNota nota = JsonSerializer.Deserialize<ResponseNota>(jsonString);

            return nota.responseXML;


        }
    }

    public class ResponseNota
    {
        public string responseXML { get; set; }
    }
}
