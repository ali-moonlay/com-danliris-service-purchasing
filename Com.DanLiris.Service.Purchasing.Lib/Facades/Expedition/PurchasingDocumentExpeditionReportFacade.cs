using Com.DanLiris.Service.Purchasing.Lib.Enums;
using Com.DanLiris.Service.Purchasing.Lib.Helpers.ReadResponse;
using Com.DanLiris.Service.Purchasing.Lib.Models.Expedition;
using Com.DanLiris.Service.Purchasing.Lib.ViewModels.Expedition;
using Com.Moonlay.NetCore.Lib;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Com.DanLiris.Service.Purchasing.Lib.Facades.Expedition
{
    public class PurchasingDocumentExpeditionReportFacade
    {
        private readonly PurchasingDbContext dbContext;
        private readonly DbSet<PurchasingDocumentExpedition> dbSet;
        public PurchasingDocumentExpeditionReportFacade(PurchasingDbContext dbContext)
        {
            this.dbContext = dbContext;
            this.dbSet = this.dbContext.Set<PurchasingDocumentExpedition>();
        }

        public ReadResponse<PurchasingDocumentExpeditionReportViewModel> GetReport(string no, string supplierCode, string divisionCode, DateTimeOffset? dateFrom, DateTimeOffset? dateTo, int? status, int page, int size)
        {
            var unitPaymentOrders = dbContext.UnitPaymentOrders.AsQueryable();

            if (!string.IsNullOrEmpty(no))
            {
                unitPaymentOrders = unitPaymentOrders.Where(x => x.UPONo == no);
            }

            if (!string.IsNullOrEmpty(supplierCode))
            {
                unitPaymentOrders = unitPaymentOrders.Where(x => x.SupplierCode == supplierCode);
            }

            if (!string.IsNullOrEmpty(divisionCode))
            {
                unitPaymentOrders = unitPaymentOrders.Where(x => x.DivisionCode == divisionCode);
            }

            if(status.HasValue)
            {
                unitPaymentOrders = unitPaymentOrders.Where(x => x.Position == status.Value);
            }

            if(dateFrom.HasValue && dateTo.HasValue)
            {
                unitPaymentOrders = unitPaymentOrders.Where(x => dateFrom <= x.Date && x.Date <= dateTo);
            }
            var upos = unitPaymentOrders.ToList();

            var data = this.dbSet
                .Select(s => new PurchasingDocumentExpedition
                {
                    UnitPaymentOrderNo = s.UnitPaymentOrderNo,
                    SendToVerificationDivisionDate = s.SendToVerificationDivisionDate,
                    VerificationDivisionDate = s.VerificationDivisionDate,
                    VerifyDate = s.VerifyDate,
                    SendToCashierDivisionDate = s.SendToCashierDivisionDate,
                    SendToAccountingDivisionDate = s.SendToAccountingDivisionDate,
                    SendToPurchasingDivisionDate = s.SendToPurchasingDivisionDate,
                    CashierDivisionDate = s.CashierDivisionDate,
                    BankExpenditureNoteDate = s.BankExpenditureNoteDate,
                    BankExpenditureNoteNo = s.BankExpenditureNoteNo,
                    BankExpenditureNotePPHDate = s.BankExpenditureNotePPHDate,
                    BankExpenditureNotePPHNo = s.BankExpenditureNotePPHNo,
                    Position = s.Position,
                    InvoiceNo = s.InvoiceNo,
                    SupplierName = s.SupplierName,
                    DivisionName = s.DivisionName,
                    UPODate = s.UPODate,
                    DueDate = s.DueDate
                })
                .Where(p => upos.Any(x => x.UPONo == p.UnitPaymentOrderNo));

            List<PurchasingDocumentExpeditionReportViewModel> list = new List<PurchasingDocumentExpeditionReportViewModel>();

            foreach(PurchasingDocumentExpedition d in data)
            {
                PurchasingDocumentExpeditionReportViewModel item = new PurchasingDocumentExpeditionReportViewModel()
                {
                    Date = d.UPODate,
                    DivisionName = d.DivisionName,
                    DueDate = d.DueDate,
                    SupplierName = d.SupplierName,
                    InvoiceNo = d.InvoiceNo,
                    SendToVerificationDivisionDate = d.SendToVerificationDivisionDate,
                    VerificationDivisionDate = d.VerificationDivisionDate,
                    VerifyDate = d.VerifyDate,
                    SendDate = (d.Position == ExpeditionPosition.CASHIER_DIVISION || d.Position == ExpeditionPosition.SEND_TO_CASHIER_DIVISION) ? d.SendToCashierDivisionDate :
                    (d.Position == ExpeditionPosition.FINANCE_DIVISION || d.Position == ExpeditionPosition.SEND_TO_ACCOUNTING_DIVISION) ? d.SendToAccountingDivisionDate:
                    (d.Position == ExpeditionPosition.SEND_TO_PURCHASING_DIVISION) ? d.SendToPurchasingDivisionDate : null,
                    CashierDivisionDate = d.CashierDivisionDate,
                    UnitPaymentOrderNo = d.UnitPaymentOrderNo,
                    BankExpenditureNoteDate = d.BankExpenditureNoteDate,
                    BankExpenditureNoteNo = d.BankExpenditureNoteNo,
                    BankExpenditureNotePPHDate = d.BankExpenditureNotePPHDate,
                    BankExpenditureNotePPHNo = d.BankExpenditureNotePPHNo,
                    Position = d.Position
                };

                list.Add(item);
            }
            Pageable<PurchasingDocumentExpeditionReportViewModel> pageable = new Pageable<PurchasingDocumentExpeditionReportViewModel>(list, page - 1, size);
            List<PurchasingDocumentExpeditionReportViewModel> Data = pageable.Data.ToList();
            int TotalData = pageable.TotalCount;
            return new ReadResponse<PurchasingDocumentExpeditionReportViewModel>(Data, TotalData, new Dictionary<string, string>());
        }
    }
}
