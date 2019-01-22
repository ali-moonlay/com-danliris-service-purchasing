using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Com.DanLiris.Service.Purchasing.Lib.ViewModels.Expedition;
using Com.DanLiris.Service.Purchasing.WebApi.Helpers;
using Com.DanLiris.Service.Purchasing.Lib.Facades.Expedition;
using Com.DanLiris.Service.Purchasing.Lib.Helpers.ReadResponse;

namespace Com.DanLiris.Service.Purchasing.WebApi.Controllers.v1.Expedition
{
    [Produces("application/json")]
    [ApiVersion("1.0")]
    [Route("v{version:apiVersion}/expedition/purchasing-document-expeditions-report")]
    [Authorize]
    public class PurchasingDocumentExpeditionReportController : Controller
    {
        private string ApiVersion = "1.0.0";
        private readonly PurchasingDocumentExpeditionReportFacade purchasingDocumentExpeditionReportFacade;

        public PurchasingDocumentExpeditionReportController(PurchasingDocumentExpeditionReportFacade purchasingDocumentExpeditionReportFacade)
        {
            this.purchasingDocumentExpeditionReportFacade = purchasingDocumentExpeditionReportFacade;
        }

        [HttpGet]
        public ActionResult Get(string no, string supplierCode, string divisionCode, DateTimeOffset? dateFrom = null, DateTimeOffset? dateTo = null, int? status = null, int page = 1, int size = 25)
        {
            ReadResponse<PurchasingDocumentExpeditionReportViewModel> Data = this.purchasingDocumentExpeditionReportFacade.GetReport(no, supplierCode, divisionCode, dateFrom, dateTo, status, page, size);

            return Ok(new
            {
                apiVersion = ApiVersion,
                data = Data.Data,
                info = new Dictionary<string, object>
                {
                    { "count", Data.Data.Count },
                    { "total", Data.TotalData },
                    { "order", Data.Order },
                    { "page", page },
                    { "size", size }
                },
                message = General.OK_MESSAGE,
                statusCode = General.OK_STATUS_CODE
            });
        }
    }
}