using Com.DanLiris.Service.Purchasing.Lib.Facades.Expedition;
using Com.DanLiris.Service.Purchasing.Lib.Models.Expedition;
using Com.DanLiris.Service.Purchasing.Lib.Services;
using Com.DanLiris.Service.Purchasing.Test.DataUtils.ExpeditionDataUtil;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Com.DanLiris.Service.Purchasing.Test.Facades.PurchasingDocumentExpeditionTest
{
    [Collection("ServiceProviderFixture Collection")]
    public class Report
    {
        private IServiceProvider ServiceProvider { get; set; }

        public Report(ServiceProviderFixture fixture)
        {
            ServiceProvider = fixture.ServiceProvider;

            IdentityService identityService = (IdentityService)ServiceProvider.GetService(typeof(IdentityService));
            identityService.Username = "Unit Test";
        }

        private SendToVerificationDataUtil DataUtil
        {
            get { return (SendToVerificationDataUtil)ServiceProvider.GetService(typeof(SendToVerificationDataUtil)); }
        }

        private PurchasingDocumentExpeditionReportFacade Facade
        {
            get { return (PurchasingDocumentExpeditionReportFacade)ServiceProvider.GetService(typeof(PurchasingDocumentExpeditionReportFacade)); }
        }

        [Fact]
        public async void Should_Success_Get_Report_Data()
        {
            PurchasingDocumentExpedition model = await DataUtil.GetTestData();
            List<string> unitPaymentOrders = new List<string>() { model.UnitPaymentOrderNo };
            var Response = this.Facade.GetReport(model.UnitPaymentOrderNo, model.SupplierCode, model.DivisionCode, model.UPODate.AddDays(-1), model.UPODate.AddDays(1), (int)model.Position, 1, 25);
            Assert.NotNull(Response);
        }
    }
}
