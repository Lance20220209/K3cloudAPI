using Kingdee.BOS;
using Kingdee.BOS.App;
using Kingdee.BOS.Contracts;
using Kingdee.BOS.Core;
using Kingdee.BOS.Core.Bill;
using Kingdee.BOS.Core.DynamicForm;
using Kingdee.BOS.Core.DynamicForm.PlugIn;
using Kingdee.BOS.Core.Metadata;
using Kingdee.BOS.Core.Metadata.FormElement;
using Kingdee.BOS.Orm;
using Kingdee.BOS.ServiceHelper;
using Kingdee.BOS.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;

namespace AnyCubicK3cloudProject
{
    [Description("每天晚上一点全部同步下数据")]
    [Kingdee.BOS.Util.HotUpdate]
    public class ZWLF_GetAllIScheduleService : IScheduleService
    {
        /// <summary>
        /// 定时任务
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="schedule"></param>
        public void Run(Context ctx, Schedule schedule)
        {
            try
            {
                ZWLF_GetSaleOrder zWLF_GetSale = new ZWLF_GetSaleOrder();
                ZWLF_HKGetSaleOrder zWLF_HKGetSaleOrder = new ZWLF_HKGetSaleOrder();
                ZWLF_GetAmazonOrder zWLF_GetAmazonOrder = new ZWLF_GetAmazonOrder();
                ZWLF_GetAssembly zWLF_GetAssembly = new ZWLF_GetAssembly();
                ZWLF_GetOtherIn zWLF_GetOtherIn = new ZWLF_GetOtherIn();
                ZWLF_GetOtherOut zWLF_GetOtherOut = new ZWLF_GetOtherOut();
                ZWLF_GetSalesReturn wLF_GetSalesReturn = new ZWLF_GetSalesReturn();
                STLib sTLib = new STLib();
                Parameters parameters = new Parameters();
                Msg msg = new Msg();
                //配置表
                string sql = string.Format(@"/*dialect*/ select top 1 app_key,AppSecret,date_type,end_date,page_no,page_size,
                                     username,password,url,wh_code ,method,start_date,access_token
                                    from ZWLF_T_Configuration where  IsDisable=0");
                sql += string.Format(@"/*dialect*/select  method  from ZWLF_T_Configuration where method  not in ('frdas.erp.moveout.get','frdas.erp.movein.get') group by method");
                DataSet ds = DBServiceHelper.ExecuteDataSet(ctx, sql);
                DataTable dt = ds.Tables[0];
                DataTable dt2 = ds.Tables[1];
                if (dt.Rows.Count > 0)
                {
                    for (int i = 0; i < dt.Rows.Count; i++)
                    {
                        parameters.app_key = dt.Rows[i]["app_key"].ToString();
                        parameters.AppSecret = dt.Rows[i]["AppSecret"].ToString();
                        parameters.page_size = "50";
                        parameters.username = dt.Rows[i]["username"].ToString();
                        parameters.password = dt.Rows[i]["password"].ToString();
                        parameters.start_date = DateTime.Now.AddDays(-1).ToString();
                        parameters.end_date = DateTime.Now.ToString();
                        parameters.page_no = Convert.ToInt32(dt.Rows[i]["page_no"].ToString());
                        parameters.url = dt.Rows[i]["url"].ToString();
                        //获取token
                        parameters.access_token = dt.Rows[i]["access_token"].ToString();
                    }
                    for (int j = 0; j < dt2.Rows.Count; j++)
                    {
                        //组装拆卸单
                        if (dt2.Rows[j]["method"].ToString() == "frdas.erp.asm.get")
                        {
                            parameters.method = dt2.Rows[j]["method"].ToString();
                            msg = zWLF_GetAssembly.GetAssembly(parameters, ctx);
                            if (msg.status)
                            {
                                for (int a = 0; a < 10000; a++)
                                {
                                    if (msg.sum == 50)
                                    {
                                        parameters.page_no = parameters.page_no + 1;
                                        msg = zWLF_GetAssembly.GetAssembly(parameters, ctx);
                                    }
                                    else
                                    {
                                        a = 10000;
                                        break;
                                    }
                                }
                            }
                        }
                        //其他入库单
                        else if (dt2.Rows[j]["method"].ToString() == "frdas.erp.otherin.get")
                        {
                            parameters.method = dt2.Rows[j]["method"].ToString();
                            msg = zWLF_GetOtherIn.GetOtherIn(parameters, ctx);
                            if (msg.status)
                            {
                                for (int a = 0; a < 1000; a++)
                                {
                                    if (msg.sum == 50)
                                    {
                                        parameters.page_no = parameters.page_no + 1;
                                        msg = zWLF_GetOtherIn.GetOtherIn(parameters, ctx);
                                    }
                                    else
                                    {
                                        a = 1000;
                                        break;
                                    }
                                }
                            }
                        }
                        //其他出库单
                        else if (dt2.Rows[j]["method"].ToString() == "frdas.erp.otherout.get")
                        {
                            parameters.method = dt2.Rows[j]["method"].ToString();
                            msg = zWLF_GetOtherOut.GetOtherOut(parameters, ctx);
                            if (msg.status)
                            {
                                for (int a = 0; a < 1000; a++)
                                {
                                    if (msg.sum == 50)
                                    {
                                        parameters.page_no = parameters.page_no + 1;
                                        msg = zWLF_GetOtherOut.GetOtherOut(parameters, ctx);
                                    }
                                    else
                                    {
                                        a = 1000;
                                        break;
                                    }
                                }
                            }
                        }
                        //销售退货
                        else if  (dt2.Rows[j]["method"].ToString() == "frdas.erp.refunds.get"|| dt2.Rows[j]["method"].ToString() == "frdas.erp.refunds.getcloud")
                        {
                            parameters.method = dt2.Rows[j]["method"].ToString();
                            msg = wLF_GetSalesReturn.SaleReturnGet(parameters, ctx);
                            if (msg.status)
                            {
                                for (int a = 0; a < 1000; a++)
                                {
                                    if (msg.sum == 50)
                                    {
                                        parameters.page_no = parameters.page_no + 1;
                                        msg = wLF_GetSalesReturn.SaleReturnGet(parameters, ctx);
                                    }
                                    else
                                    {
                                        a = 1000;
                                        break;
                                    }
                                }
                            }
                        }
                        else if (dt2.Rows[j]["method"].ToString() == "frdas.erp.saleout.get")
                        {
                            parameters.method = dt2.Rows[j]["method"].ToString();
                            //获取亚马逊
                            msg = zWLF_GetAmazonOrder.SaleTradeGet(parameters, ctx);
                            if (msg.status)
                            {
                                for (int a = 0; a < 1000; a++)
                                {
                                    if (msg.sum == 50)
                                    {
                                        parameters.page_no = parameters.page_no + 1;
                                        msg = zWLF_GetAmazonOrder.SaleTradeGet(parameters, ctx);
                                    }
                                    else
                                    {
                                        a = 1000;
                                        break;
                                    }
                                }
                            }
                            //获取海外仓数据
                            msg = zWLF_HKGetSaleOrder.SaleTradeGet(parameters, ctx);
                            if (msg.status)
                            {
                                for (int a = 0; a < 1000; a++)
                                {
                                    if (msg.sum == 50)
                                    {
                                        parameters.page_no = parameters.page_no + 1;
                                        msg = zWLF_HKGetSaleOrder.SaleTradeGet(parameters, ctx);
                                    }
                                    else
                                    {
                                        a = 1000;
                                        break;
                                    }
                                }
                            }
                            //获取本地仓销售数据
                            msg = zWLF_GetSale.SaleTradeGet(parameters, ctx);
                            if (msg.status)
                            {
                                for (int a = 0; a < 1000; a++)
                                {
                                    if (msg.sum == 50)
                                    {
                                        parameters.page_no = parameters.page_no + 1;
                                        msg = zWLF_GetSale.SaleTradeGet(parameters, ctx);
                                    }
                                    else
                                    {
                                        a = 1000;
                                        break;
                                    }
                                }
                            }
                        }
                        //富润销售数据
                        else if (dt2.Rows[j]["method"].ToString() == "frdas.erp.saleout.getcloud")
                        {
                            parameters.method = dt2.Rows[j]["method"].ToString();
                            //获取本地仓销售数据
                            msg = zWLF_GetSale.SaleTradeGet(parameters, ctx);
                            if (msg.status)
                            {
                                for (int a = 0; a < 1000; a++)
                                {
                                    if (msg.sum == 50)
                                    {
                                        parameters.page_no = parameters.page_no + 1;
                                        msg = zWLF_GetSale.SaleTradeGet(parameters, ctx);
                                    }
                                    else
                                    {
                                        a = 1000;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (KDException ex)
            {
                throw new KDException("", ex.ToString());
            }
        }
    }
}
