using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Hosting;
using System.Web.Mvc;
using SwishApiTester.Helpers;
using System.Security.Cryptography.X509Certificates;
using Client;
using System.Threading.Tasks;
using System.Net;
using System.Threading;

namespace SwishApiTester.Controllers
{
    public class TestController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<ActionResult> Swish(FormCollection f)
        {
            string payeePaymentReference = f["inputReference"];
            string payerAlias = f["inputBuyerNumber"];
            int amount = Convert.ToInt32(f["inputAmountr"]);
            string message = f["inputMessage"];

            var client = GetSwishClient();

            // Make payment
            var ecommercePaymentModel = new ECommercePaymentModel(
                amount: amount.ToString(),
                currency: "SEK",
                callbackUrl: Config.Test.CallbackURL,
                payerAlias: payerAlias)
            {
                PayeePaymentReference = payeePaymentReference,
                Message = message
            };

            var paymentResponse = await client.MakeECommercePaymentAsync(ecommercePaymentModel);

            ViewBag.result = paymentResponse;

            return View();
        }

        [HttpPost]
        public async Task<ActionResult> PaymentStatus(string id)
        {
            Thread.Sleep(5000);

            var client = GetSwishClient();

            // Check payment request status
            var paymentStatus = await client.GetPaymentStatus(id);

            // When someone like to use this live i should log this and maybe change the status of some order or somethign to be paid or what the status says.
            // To make a refund you need to save the value of paymentReference
            // var paymentReference = result.paymentReference;

            return Json(paymentStatus, JsonRequestBehavior.DenyGet);
        }

        public string Callback()
        {
            Stream req = Request.InputStream;
            req.Seek(0, System.IO.SeekOrigin.Begin);
            string json = new StreamReader(req).ReadToEnd();

            SwishCheckPaymentRequestStatusResponse resultObject = JsonConvert.DeserializeObject<SwishCheckPaymentRequestStatusResponse>(json);

            switch (resultObject.status)
            {
                case "CREATED":
                    // Borde kanske alldrig få CREATED här...
                    break;
                case "PAID":
                    // Betalningen är klar
                    break;
                case "DECLINED":
                    // Användaren avbröt betalningen
                    break;
                case "ERROR":
                    // Något gick fel, om betalningen inte sker inom 3 minuter skickas ERROR
                    break;
            }

            // When someone like to use this live i should log this and maybe change the status of some order or somethign to be paid or what the status says.
            // To make a refund you need to save the value of paymentReference
            // var paymentReference = resultObject.paymentReference;
            return "OK";
        }




        /// <summary>
        /// Refund a payment with paymentReference from a payment status that says PAID
        /// </summary>
        /// <param name="id">paymentReference</param>
        /// <param name="a">Amount to refund</param>
        /// <param name="p">payeePaymentReference = Order ID eller annat som identifierar ordern som är betald och i detta fall återköpt</param>
        /// <returns></returns>
        public ActionResult Refund(string id, int a, string p)
        {
            string result = string.Empty;

            // Kontrollera om web.config är satt att vara i test läge
            if (Config.TestMode)
            {
                // Hämta sökvägen till certifikatet
                string certificatePath = HostingEnvironment.MapPath(@"~/App_Data/" + Config.Test.CertificateFileName);

                // Läs in certifikat filen som en byte array, detta är endast för att visa att funktionen nedan kan ta en byte array som skulle kunna hämtats från en databas
                byte[] certDataBytes = System.IO.File.ReadAllBytes(certificatePath);

                // Kontrollera betalnings status med hjälp av certifikatet som en byte array
                // "Återköp" strängen är meddelandet användaren ser vid återbetalning i Swish appen, här bör man kankse i en produktionsmiljö skicka med mer detaljer
                result = SwishHelper.PaymentRefundWithWithByteArray(p, id, a, "Återköp", Config.Test.RefundCallbackURL, certDataBytes, Config.Test.CertificatePassword, Config.Test.PaymentRefundURL, Config.Test.PayeeAlias);
            }
            else
            {
                // Hämta sökvägen till certifikatet
                string certificatePath = HostingEnvironment.MapPath(@"~/App_Data/" + Config.Production.CertificateFileName);

                // Läs in certifikat filen som en byte array, detta är endast för att visa att funktionen nedan kan ta en byte array som skulle kunna hämtats från en databas
                byte[] certDataBytes = System.IO.File.ReadAllBytes(certificatePath);

                // Kontrollera betalnings status med hjälp av certifikatet som en byte array
                // "Återköp" strängen är meddelandet användaren ser vid återbetalning i Swish appen, här bör man kankse i en produktionsmiljö skicka med mer detaljer
                result = SwishHelper.PaymentRefundWithWithByteArray(p, id, a, "Återköp", Config.Production.RefundCallbackURL, certDataBytes, Config.Production.CertificatePassword, Config.Production.PaymentRefundURL, Config.Production.PayeeAlias);
            }

            return View();
        }

        public string RefundCallback()
        {
            // Exempel Callback json sträng
            // 
            Stream req = Request.InputStream;
            req.Seek(0, System.IO.SeekOrigin.Begin);
            string json = new StreamReader(req).ReadToEnd();

            //logger.Debug("/Test/RefundCallback > json: " + json);

            SwishRefundSatusCheckResponse resultObject = JsonConvert.DeserializeObject<SwishRefundSatusCheckResponse>(json);

            switch (resultObject.status)
            {
                case "DEBITED,":
                    // Återköpt
                    break;
                case "PAID":
                    // Betald
                    break;
                case "ERROR":
                    // Något gick fel
                    break;
            }

            // When someone like to use this live i should log this and maybe change the status of some order or something to be repaid or what the status says.
            // Use payerPaymentReference to get the order
            // var paymentref = resultObject.payerPaymentReference;

            return "OK";
        }

        private X509Certificate2 GetX509Certificate2ByX(string certificateFileName, string certificatePassword = "")
        {
            //Basic set up 
            ServicePointManager.CheckCertificateRevocationList = false;

            //Tls12 does not work 
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls11;

            //Load client certificates 
            var clientCerts = new X509Certificate2Collection();

            // Hämta sökvägen till certifikatet
            string certificatePath = HostingEnvironment.MapPath(certificateFileName);

            // Läs in certifikat filen som en byte array, detta är endast för att visa att funktionen nedan kan ta en byte array som skulle kunna hämtats från en databas
            byte[] certDataBytes = System.IO.File.ReadAllBytes(certificatePath);

            clientCerts.Import(certDataBytes, certificatePassword, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.PersistKeySet);

            //Assert CA certs in cert store, and get root CA 
            X509Certificate2 rootCertificate = SwishHelper.AssertCertsInStore(clientCerts);

            return rootCertificate;
        }

        private SwishClient GetSwishClient()
        {
            var configuration = new TestConfig(Config.Test.PayeeAlias);
            var serverMapPath = Server.MapPath(@"~/App_Data/");
            var clientCert = new X509Certificate2(serverMapPath + Config.Test.CertificateFileName, Config.Test.CertificatePassword);
            var caCert = new X509Certificate2(serverMapPath + "Swish_TLS_RootCA.pem");

            var client = new SwishClient(configuration, clientCert, caCert);
            return client;
        }
    }
}