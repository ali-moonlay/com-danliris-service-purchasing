using Com.DanLiris.Service.Purchasing.Lib.Helpers;
using Com.DanLiris.Service.Purchasing.Lib.Interfaces;
using Com.DanLiris.Service.Purchasing.Lib.Models.DeliveryOrderModel;
using Com.DanLiris.Service.Purchasing.Lib.Models.ExternalPurchaseOrderModel;
using Com.DanLiris.Service.Purchasing.Lib.Models.InternalPurchaseOrderModel;
using Com.DanLiris.Service.Purchasing.Lib.Models.PurchaseRequestModel;
using Com.DanLiris.Service.Purchasing.Lib.Models.UnitPaymentOrderModel;
using Com.DanLiris.Service.Purchasing.Lib.Models.UnitReceiptNoteModel;
using Com.Moonlay.Models;
using Com.Moonlay.NetCore.Lib;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Com.DanLiris.Service.Purchasing.Lib.Facades
{
    public class UnitPaymentOrderFacade : IUnitPaymentOrderFacade
    {
        private readonly PurchasingDbContext dbContext;
        private readonly DbSet<UnitPaymentOrder> dbSet;

        private string USER_AGENT = "Facade";

        public UnitPaymentOrderFacade(PurchasingDbContext dbContext)
        {
            this.dbContext = dbContext;
            this.dbSet = dbContext.Set<UnitPaymentOrder>();
        }

        public Tuple<List<UnitPaymentOrder>, int, Dictionary<string, string>> Read(int Page = 1, int Size = 25, string Order = "{}", string Keyword = null, string Filter = "{}")
        {
            IQueryable<UnitPaymentOrder> Query = this.dbSet;

            List<string> searchAttributes = new List<string>()
            {
                "UPONo", "DivisionName", "SupplierName", "Items.URNNo", "Items.DONo"
            };

            Query = QueryHelper<UnitPaymentOrder>.ConfigureSearch(Query, searchAttributes, Keyword);

            Query = Query.Select(s => new UnitPaymentOrder
            {
                Id = s.Id,
                Position = s.Position,
                DivisionId = s.DivisionId,
                DivisionCode = s.DivisionCode,
                DivisionName = s.DivisionName,
                SupplierId = s.SupplierId,
                SupplierCode = s.SupplierCode,
                SupplierName = s.SupplierName,
                CategoryCode=s.CategoryCode,
                CategoryId=s.CategoryId,
                CategoryName=s.CategoryName,
                Date = s.Date,
                UPONo = s.UPONo,
                DueDate=s.DueDate,
                UseIncomeTax=s.UseIncomeTax,
                UseVat=s.UseVat,
                CurrencyCode=s.CurrencyCode,
                CurrencyDescription=s.CurrencyDescription,
                CurrencyId=s.CurrencyId,
                CurrencyRate=s.CurrencyRate,
                Items = s.Items.Select(i => new UnitPaymentOrderItem
                {
                    URNNo = i.URNNo,
                    DONo = i.DONo,
                    Details=i.Details.ToList()
                }).ToList(),
                CreatedBy = s.CreatedBy,
                LastModifiedUtc = s.LastModifiedUtc,
            });

            Dictionary<string, string> FilterDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(Filter);
            Query = QueryHelper<UnitPaymentOrder>.ConfigureFilter(Query, FilterDictionary);

            Dictionary<string, string> OrderDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(Order);
            Query = QueryHelper<UnitPaymentOrder>.ConfigureOrder(Query, OrderDictionary);

            Pageable<UnitPaymentOrder> pageable = new Pageable<UnitPaymentOrder>(Query, Page - 1, Size);
            List<UnitPaymentOrder> Data = pageable.Data.ToList();
            int TotalData = pageable.TotalCount;

            return Tuple.Create(Data, TotalData, OrderDictionary);
        }

        public UnitPaymentOrder ReadById(int id)
        {
            var Result = dbSet.Where(m => m.Id == id)
                .Include(m => m.Items)
                    .ThenInclude(i => i.Details)
                .FirstOrDefault();

            return Result;
        }

        public async Task<int> Create(UnitPaymentOrder model, string user, bool isImport, int clientTimeZoneOffset = 7)
        {
            int Created = 0;

            using(var transaction = dbContext.Database.BeginTransaction())
            {
                try
                {
                    EntityExtension.FlagForCreate(model, user, USER_AGENT);
                    model.UPONo = await GenerateNo(model, isImport, clientTimeZoneOffset);
                    foreach (var item in model.Items)
                    {
                        EntityExtension.FlagForCreate(item, user, USER_AGENT);
                        foreach (var detail in item.Details)
                        {
                            SetPOItemIdEPONo(detail);
                            EntityExtension.FlagForCreate(detail, user, USER_AGENT);
                        }
                        SetPaid(item, true, user);
                    }

                    SetDueDate(model);

                    this.dbSet.Add(model);

                    Created = await dbContext.SaveChangesAsync();

                    foreach (var item in model.Items)
                    {
                        foreach (var detail in item.Details)
                        {
                            SetStatus(detail, user);
                        }
                    }

                    await dbContext.SaveChangesAsync();
                    transaction.Commit();
                }
                catch (Exception e)
                {
                    transaction.Rollback();
                    throw new Exception(e.Message);
                }
            }

            return Created;
        }

        public async Task<int> Update(int id, UnitPaymentOrder model, string user)
        {
            int Updated = 0;

            using (var transaction = this.dbContext.Database.BeginTransaction())
            {
                try
                {
                    var existingModel = this.dbSet.AsNoTracking()
                        .Include(d => d.Items)
                            .ThenInclude(d => d.Details)
                        .SingleOrDefault(m => m.Id == id && !m.IsDeleted);

                    if (existingModel != null && id == model.Id)
                    {
                        EntityExtension.FlagForUpdate(model, user, USER_AGENT);

                        foreach (var item in model.Items)
                        {
                            if (item.Id == 0)
                            {
                                EntityExtension.FlagForCreate(item, user, USER_AGENT);
                                foreach (var detail in item.Details)
                                {
                                    SetPOItemIdEPONo(detail);
                                    EntityExtension.FlagForCreate(detail, user, USER_AGENT);
                                }
                            }
                            else
                            {
                                EntityExtension.FlagForUpdate(item, user, USER_AGENT);
                                foreach (var detail in item.Details)
                                {
                                    EntityExtension.FlagForUpdate(detail, user, USER_AGENT);
                                }
                            }

                            SetPaid(item, true, user);
                        }

                        SetDueDate(model);

                        this.dbContext.Update(model);

                        foreach (var existingItem in existingModel.Items)
                        {
                            var newItem = model.Items.FirstOrDefault(i => i.Id == existingItem.Id);
                            if (newItem == null)
                            {
                                EntityExtension.FlagForDelete(existingItem, user, USER_AGENT);
                                this.dbContext.UnitPaymentOrderItems.Update(existingItem);
                                foreach (var existingDetail in existingItem.Details)
                                {
                                    EntityExtension.FlagForDelete(existingDetail, user, USER_AGENT);
                                    this.dbContext.UnitPaymentOrderDetails.Update(existingDetail);
                                }

                                SetPaid(existingItem, false, user);
                            }
                        }

                        Updated = await dbContext.SaveChangesAsync();

                        foreach (var item in model.Items)
                        {
                            foreach (var detail in item.Details)
                            {
                                SetStatus(detail, user);
                            }
                        }

                        await dbContext.SaveChangesAsync();
                        transaction.Commit();
                    }
                    else
                    {
                        throw new Exception("Invalid Id");
                    }
                }
                catch (Exception e)
                {
                    transaction.Rollback();
                    throw new Exception(e.Message);
                }
            }

            return Updated;
        }

        public async Task<int> Delete(int id, string user)
        {
            int Deleted = 0;

            using (var transaction = this.dbContext.Database.BeginTransaction())
            {
                try
                {
                    var model = this.dbSet
                        .Include(d => d.Items)
                            .ThenInclude(d => d.Details)
                        .SingleOrDefault(m => m.Id == id && !m.IsDeleted);

                    EntityExtension.FlagForDelete(model, user, USER_AGENT);

                    foreach (var item in model.Items)
                    {
                        EntityExtension.FlagForDelete(item, user, USER_AGENT);
                        foreach (var detail in item.Details)
                        {
                            EntityExtension.FlagForDelete(detail, user, USER_AGENT);
                        }

                        SetPaid(item, false, user);
                    }

                    Deleted = await dbContext.SaveChangesAsync();

                    foreach (var item in model.Items)
                    {
                        foreach (var detail in item.Details)
                        {
                            SetStatus(detail, user);
                        }
                    }

                    await dbContext.SaveChangesAsync();
                    transaction.Commit();
                }
                catch (Exception e)
                {
                    transaction.Rollback();
                    throw new Exception(e.Message);
                }
            }

            return Deleted;
        }

        async Task<string> GenerateNo(UnitPaymentOrder model, bool isImport, int clientTimeZoneOffset)
        {
            string Year = model.Date.ToOffset(new TimeSpan(clientTimeZoneOffset, 0, 0)).ToString("yy");
            string Month = model.Date.ToOffset(new TimeSpan(clientTimeZoneOffset, 0, 0)).ToString("MM");
            string Supplier = isImport ? "NKI" : "NKL";
            string TG = "";
            if (model.DivisionName.ToUpper().Equals("GARMENT"))
            {
                TG = "G-";
            }
            else if(model.DivisionName.ToUpper().Equals("UMUM") || 
                model.DivisionName.ToUpper().Equals("SPINNING") ||
                model.DivisionName.ToUpper().Equals("FINISHING & PRINTING") ||
                model.DivisionName.ToUpper().Equals("UTILITY") ||
                model.DivisionName.ToUpper().Equals("WEAVING"))
            {
                TG = "T-";
            }

            string no = $"{Year}-{Month}-{TG}{Supplier}-";
            int Padding = isImport ? 3 : 4;

            var lastNo = await dbSet.Where(w => w.UPONo.StartsWith(no) && !w.IsDeleted).OrderByDescending(o => o.UPONo).FirstOrDefaultAsync();

            if (lastNo == null)
            {
                return no + "1".PadLeft(Padding, '0');
            }
            else
            {
                int lastNoNumber = Int32.Parse(lastNo.UPONo.Replace(no, "")) + 1;
                return no + lastNoNumber.ToString().PadLeft(Padding, '0');
            }
        }

        private void SetPOItemIdEPONo(UnitPaymentOrderDetail detail)
        {
            ExternalPurchaseOrderDetail EPODetail = dbContext.ExternalPurchaseOrderDetails.Single(m => m.Id == detail.EPODetailId);
            detail.POItemId = EPODetail.POItemId;

            detail.EPONo = dbContext.ExternalPurchaseOrders.Single(m => m.Items.Any(i => i.Id == EPODetail.EPOItemId)).EPONo;
        }

        private void SetPaid(UnitPaymentOrderItem item, bool isPaid, string username)
        {
            UnitReceiptNote unitReceiptNote = dbContext.UnitReceiptNotes.Single(m => m.Id == item.URNId);
            unitReceiptNote.IsPaid = isPaid;
            EntityExtension.FlagForUpdate(unitReceiptNote, username, USER_AGENT);
        }

        private void SetStatus(UnitPaymentOrderDetail detail, string username)
        {
            ExternalPurchaseOrderDetail EPODetail = dbContext.ExternalPurchaseOrderDetails.Single(m => m.Id == detail.EPODetailId);
            InternalPurchaseOrderItem POItem = dbContext.InternalPurchaseOrderItems.Single(m => m.Id == EPODetail.POItemId);

            List<long> EPODetailIds = dbContext.ExternalPurchaseOrderDetails.Where(m => m.POItemId == POItem.Id).Select(m => m.Id).ToList();
            List<long> URNItemIds = dbContext.UnitReceiptNoteItems.Where(m => EPODetailIds.Contains(m.EPODetailId)).Select(m => m.Id).ToList();

            var totalReceiptQuantity = dbContext.UnitPaymentOrderDetails.AsNoTracking().Where(m => m.IsDeleted == false && URNItemIds.Contains(m.URNItemId)).Sum(m => m.ReceiptQuantity);
            if (totalReceiptQuantity > 0)
            {
                if (totalReceiptQuantity < EPODetail.DealQuantity)
                {
                    POItem.Status = "Sudah dibuat SPB sebagian";
                }
                else
                {
                    POItem.Status = "Sudah dibuat SPB semua";
                }
            }
            else if (totalReceiptQuantity == 0)
            {
                if (EPODetail.DOQuantity >= EPODetail.DealQuantity)
                {
                    if (EPODetail.ReceiptQuantity < EPODetail.DealQuantity)
                    {
                        POItem.Status = "Barang sudah diterima Unit parsial";
                    }
                    else
                    {
                        POItem.Status = "Barang sudah diterima Unit semua";
                    }
                }
                else
                {
                    POItem.Status = "Barang sudah diterima Unit parsial";
                }
            }
            EntityExtension.FlagForUpdate(POItem, username, USER_AGENT);
        }

        private void SetDueDate(UnitPaymentOrder model)
        {
            List<DateTimeOffset> DueDates = new List<DateTimeOffset>();
            foreach (var item in model.Items)
            {
                var unitReceiptNoteDate = dbContext.UnitReceiptNotes.Single(m => m.Id == item.URNId).ReceiptDate;
                foreach (var detail in item.Details)
                {
                    var PaymentDueDays = dbContext.ExternalPurchaseOrders.Single(m => m.EPONo.Equals(detail.EPONo)).PaymentDueDays;
                    DueDates.Add(unitReceiptNoteDate.AddDays(Double.Parse(PaymentDueDays ?? "0")));
                }
            }
            model.DueDate = DueDates.Min();
        }

        public Tuple<List<UnitPaymentOrder>, int, Dictionary<string, string>> ReadSpb(int Page = 1, int Size = 25, string Order = "{}", string Keyword = null, string Filter = "{}")
        {
            IQueryable<UnitPaymentOrder> Query = this.dbSet;

            List<string> searchAttributes = new List<string>()
            {
                "UPONo", "DivisionName", "SupplierName", "Items.URNNo", "Items.DONo"
            };

            Query = QueryHelper<UnitPaymentOrder>.ConfigureSearch(Query, searchAttributes, Keyword);

            Query = Query.Select(s => new UnitPaymentOrder
            {
                Id = s.Id,
                Position = s.Position,
                DivisionId = s.DivisionId,
                DivisionCode = s.DivisionCode,
                DivisionName = s.DivisionName,
                SupplierId = s.SupplierId,
                SupplierCode = s.SupplierCode,
                SupplierName = s.SupplierName,
                CategoryId = s.CategoryId,
                CategoryCode = s.CategoryCode,
                CategoryName = s.CategoryName,
                CurrencyId = s.CurrencyId,
                CurrencyCode = s.CurrencyCode,
                CurrencyRate = s.CurrencyRate,
                CurrencyDescription = s.CurrencyDescription,
                PaymentMethod = s.PaymentMethod,
                InvoiceNo = s.InvoiceNo,
                InvoiceDate = s.InvoiceDate,
                PibNo = s.PibNo,
                UseIncomeTax = s.UseIncomeTax,
                IncomeTaxId = s.IncomeTaxId,
                IncomeTaxName = s.IncomeTaxName,
                IncomeTaxRate = s.IncomeTaxRate,
                IncomeTaxNo = s.IncomeTaxNo,
                IncomeTaxDate = s.IncomeTaxDate,
                UseVat = s.UseVat,
                VatNo = s.VatNo,
                VatDate = s.VatDate,
                Remark = s.Remark,
                DueDate = s.DueDate,
                Date = s.Date,
                UPONo = s.UPONo,
                Items = s.Items.Select(i => new UnitPaymentOrderItem
                {
                    UPOId = i.UPOId,
                    URNId = i.URNId,
                    URNNo = i.URNNo,
                    DOId = i.DOId,
                    DONo = i.DONo,
                    Details = i.Details.Select(j => new UnitPaymentOrderDetail
                    {
                        Id = j.Id,
                        UPOItemId = j.UPOItemId,
                        URNItemId = j.URNItemId,
                        EPONo = j.EPONo,
                        PRId = j.PRId,
                        PRNo = j.PRNo,
                        PRItemId = j.PRItemId,
                        ProductId = j.ProductId,
                        ProductCode = j.ProductCode,
                        ProductName = j.ProductName,
                        ProductRemark = j.ProductRemark,
                        ReceiptQuantity = j.ReceiptQuantity,
                        UomId = j.UomId,
                        UomUnit = j.UomUnit,
                        PricePerDealUnit = j.PricePerDealUnit,
                        PriceTotal = j.PriceTotal,
                        PricePerDealUnitCorrection = j.PricePerDealUnitCorrection,
                        PriceTotalCorrection = j.PriceTotalCorrection,
                        QuantityCorrection = j.QuantityCorrection,
                        //Duedate = s.DueDate,
                    }).ToList()
                }).ToList(),
                CreatedBy = s.CreatedBy,
                LastModifiedUtc = s.LastModifiedUtc,
            });

            Dictionary<string, string> FilterDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(Filter);
            Query = QueryHelper<UnitPaymentOrder>.ConfigureFilter(Query, FilterDictionary);

            Dictionary<string, string> OrderDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(Order);
            Query = QueryHelper<UnitPaymentOrder>.ConfigureOrder(Query, OrderDictionary);

            Pageable<UnitPaymentOrder> pageable = new Pageable<UnitPaymentOrder>(Query, Page - 1, Size);
            List<UnitPaymentOrder> Data = pageable.Data.ToList();
            int TotalData = pageable.TotalCount;

            return Tuple.Create(Data, TotalData, OrderDictionary);
        }

        public Tuple<List<UnitPaymentOrder>, int, Dictionary<string, string>> ReadPositionFiltered(int Page = 1, int Size = 25, string Order = "{}", string Keyword = null, string Filter = "{}")
        {
            IQueryable<UnitPaymentOrder> Query = this.dbSet;

            List<string> searchAttributes = new List<string>()
            {
                "UPONo", "DivisionName", "SupplierName", "Items.URNNo", "Items.DONo"
            };

            Query = QueryHelper<UnitPaymentOrder>.ConfigureSearch(Query, searchAttributes, Keyword);

            Query = Query.Select(s => new UnitPaymentOrder
            {
                Id = s.Id,
                DivisionId = s.DivisionId,
                DivisionCode = s.DivisionCode,
                DivisionName = s.DivisionName,
                SupplierId = s.SupplierId,
                SupplierCode = s.SupplierCode,
                SupplierName = s.SupplierName,
                CategoryId = s.CategoryId,
                CategoryCode = s.CategoryCode,
                CategoryName = s.CategoryName,
                CurrencyId = s.CurrencyId,
                CurrencyCode = s.CurrencyCode,
                CurrencyRate = s.CurrencyRate,
                CurrencyDescription = s.CurrencyDescription,
                PaymentMethod = s.PaymentMethod,
                InvoiceNo = s.InvoiceNo,
                InvoiceDate = s.InvoiceDate,
                PibNo = s.PibNo,
                UseIncomeTax = s.UseIncomeTax,
                IncomeTaxId = s.IncomeTaxId,
                IncomeTaxName = s.IncomeTaxName,
                IncomeTaxRate = s.IncomeTaxRate,
                IncomeTaxNo = s.IncomeTaxNo,
                IncomeTaxDate = s.IncomeTaxDate,
                UseVat = s.UseVat,
                VatNo = s.VatNo,
                VatDate = s.VatDate,
                Remark = s.Remark,
                DueDate = s.DueDate,
                Date = s.Date,
                UPONo = s.UPONo,
                Position = s.Position,
                Items = s.Items.Select(i => new UnitPaymentOrderItem
                {
                    UPOId = i.UPOId,
                    URNId = i.URNId,
                    URNNo = i.URNNo,
                    DOId = i.DOId,
                    DONo = i.DONo,
                    Details = i.Details.Select(j => new UnitPaymentOrderDetail
                    {
                        Id = j.Id,
                        UPOItemId = j.UPOItemId,
                        URNItemId = j.URNItemId,
                        EPONo = j.EPONo,
                        PRId = j.PRId,
                        PRNo = j.PRNo,
                        PRItemId = j.PRItemId,
                        ProductId = j.ProductId,
                        ProductCode = j.ProductCode,
                        ProductName = j.ProductName,
                        ProductRemark = j.ProductRemark,
                        ReceiptQuantity = j.ReceiptQuantity,
                        UomId = j.UomId,
                        UomUnit = j.UomUnit,
                        PricePerDealUnit = j.PricePerDealUnit,
                        PriceTotal = j.PriceTotal,
                        PricePerDealUnitCorrection = j.PricePerDealUnitCorrection,
                        PriceTotalCorrection = j.PriceTotalCorrection,
                        QuantityCorrection = j.QuantityCorrection,
                        //Duedate = s.DueDate,
                    }).ToList()
                }).ToList(),
                CreatedBy = s.CreatedBy,
                LastModifiedUtc = s.LastModifiedUtc,
            });

            Dictionary<string, List<int>> FilterDictionary = JsonConvert.DeserializeObject<Dictionary<string, List<int>>>(Filter);
            if(FilterDictionary.Keys.FirstOrDefault() == "position")
            {
                List<int> filteredPosition = FilterDictionary.GetValueOrDefault("position");
                Query = Query.Where(x => filteredPosition.Contains(x.Position));
            }

            Dictionary<string, string> OrderDictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(Order);
            Query = QueryHelper<UnitPaymentOrder>.ConfigureOrder(Query, OrderDictionary);

            Pageable<UnitPaymentOrder> pageable = new Pageable<UnitPaymentOrder>(Query, Page - 1, Size);
            List<UnitPaymentOrder> Data = pageable.Data.ToList();
            int TotalData = pageable.TotalCount;

            return Tuple.Create(Data, TotalData, OrderDictionary);
        }

        #region ForPDF

        public UnitReceiptNote GetUnitReceiptNote(long URNId)
        {
            return dbContext.UnitReceiptNotes.Single(m => m.Id == URNId);
        }

        public ExternalPurchaseOrder GetExternalPurchaseOrder(string EPONo)
        {
            return dbContext.ExternalPurchaseOrders.Single(m => m.EPONo.Equals(EPONo));
        }

        #endregion
    }
}
