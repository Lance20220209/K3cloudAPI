using Kingdee.BOS;
using Kingdee.BOS.Core.List;
using Kingdee.BOS.Core.List.PlugIn;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.ServiceHelper;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnyCubicK3cloudProject
{
    [Description("重新获取失败的订单")]
    [Kingdee.BOS.Util.HotUpdate]
    public class ZWLF_GetFailSaleOrder : AbstractListPlugIn
    {
        public override void BarItemClick(Kingdee.BOS.Core.DynamicForm.PlugIn.Args.BarItemClickEventArgs e)
        {
            base.BarItemClick(e);
            if (e.BarItemKey.Equals("ZWLF_tbGetfailOrder"))
            {
                string mssg = "";
                Context context = this.Context;
                string sql = "";
                //选择的行,获取所有信息,放在listcoll里面
                ListSelectedRowCollection listcoll = this.ListView.SelectedRowsInfo;
                if (listcoll.Count > 0)
                {
                    //接收返回的数组值
                    string[] listKey = listcoll.GetPrimaryKeyValues();
                    string ids = string.Join("','", listKey);
                    ZWLF_GetSaleOrder zWLF_GetSale = new ZWLF_GetSaleOrder();
                    ZWLF_HKGetSaleOrder zWLF_HKGetSaleOrder = new ZWLF_HKGetSaleOrder();
                    ZWLF_GetAmazonOrder zWLF_GetAmazonOrder = new ZWLF_GetAmazonOrder();
                    STLib sTLib = new STLib();
                    Parameters parameters = new Parameters();
                    Msg msg = new Msg();
                    sql = string.Format(@"/*dialect*/ select  top 1 app_key,AppSecret,date_type,end_date,page_no,page_size,
                                     username,password,url,wh_code ,method,start_date,access_token
                                    from ZWLF_T_Configuration where  IsDisable=0;");
                    sql += string.Format(@"/*dialect*/ select distinct  F_ZWLF_ID,F_ZWLF_SourceType ,F_ZWLF_IsthirdStock,F_ZWLF_IsAmazon from  ZWLF_T_OrderLog 
                                         where F_ZWLF_SourceType in ('01','06')  and  FID in ('{0}') and (F_ZWLF_Orderstate='0' or F_ZWLF_STKSTATE='0')", ids);
                    DataSet ds = DBServiceHelper.ExecuteDataSet(context, sql);
                    DataTable dt_C = ds.Tables[0];
                    DataTable dt_L = ds.Tables[1];
                    if (dt_C.Rows.Count > 0)
                    {
                        for (int j = 0; j < dt_C.Rows.Count; j++)
                        {
                            parameters.app_key = dt_C.Rows[j]["app_key"].ToString();
                            parameters.AppSecret = dt_C.Rows[j]["AppSecret"].ToString();
                            parameters.page_size = dt_C.Rows[j]["page_size"].ToString();
                            parameters.username = dt_C.Rows[j]["username"].ToString();
                            parameters.password = dt_C.Rows[j]["password"].ToString();
                            DateTime Btime = Convert.ToDateTime(dt_C.Rows[j]["start_date"].ToString()).AddMonths(-5);
                            parameters.start_date = Btime.ToString();
                            parameters.end_date = dt_C.Rows[j]["end_date"].ToString();
                            parameters.wh_code = "";
                            parameters.page_no = Convert.ToInt32(dt_C.Rows[j]["page_no"].ToString());
                            parameters.url = dt_C.Rows[j]["url"].ToString();
                            //获取token
                            //string access_token = sTLib.GetAccessToken(parameters.AppSecret, parameters.app_key, parameters.username, parameters.password);
                            parameters.access_token = dt_C.Rows[j]["access_token"].ToString();
                        }
                        if (dt_L.Rows.Count > 0)
                        {
                            //循环订单
                            for (int i = 0; i < dt_L.Rows.Count; i++)
                            {
                                //胜途订单唯一标识
                                string F_ZWLF_ID = dt_L.Rows[i]["F_ZWLF_ID"].ToString().Trim();
                                string F_ZWLF_SourceType = dt_L.Rows[i]["F_ZWLF_SourceType"].ToString().Trim();
                                string F_ZWLF_IsthirdStock= dt_L.Rows[i]["F_ZWLF_IsthirdStock"].ToString().Trim();
                                string F_ZWLF_IsAmazon= dt_L.Rows[i]["F_ZWLF_IsAmazon"].ToString().Trim();
                                if (F_ZWLF_SourceType == "01")
                                {
                                    parameters.method = "frdas.erp.saleout.get";
                                }
                                else
                                {
                                    parameters.method = "frdas.erp.saleout.getcloud";
                                }
                                parameters.id = F_ZWLF_ID;
                                try
                                {
                                  
                                    if (F_ZWLF_IsAmazon == "1") //亚马逊平台
                                    {
                                        msg = zWLF_GetAmazonOrder.SaleTradeGet(parameters, context);
                                    }
                                    else if (F_ZWLF_IsthirdStock == "0"&& F_ZWLF_IsAmazon != "1") //国内
                                    {
                                        msg = zWLF_GetSale.SaleTradeGet(parameters, context);
                                    }
                                    else if (F_ZWLF_IsthirdStock == "1" && F_ZWLF_IsAmazon != "1")//海外仓发货非亚马逊平台
                                    {
                                        msg= zWLF_HKGetSaleOrder.SaleTradeGet(parameters, context);
                                    }
                                    mssg = "重新获取：" + msg.result;
                                    sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_NOTE='{0}'+F_ZWLF_NOTE  where F_ZWLF_ID='{1}';", mssg, F_ZWLF_ID);
                                    DBServiceHelper.Execute(context, sql);
                                }
                                catch (Exception ex)
                                {
                                    mssg = ex.ToString().Substring(0, 300).Replace("'", "");
                                    //记录生成订单成功
                                    sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_NOTE='{0}'+F_ZWLF_NOTE where F_ZWLF_ID='{1}';", mssg, F_ZWLF_ID);
                                    DBServiceHelper.Execute(context, sql);
                                }
                            }
                        }
                        else
                        {
                            this.View.ShowErrMessage("请选择胜途单据类型为胜途发货单，并且(销售订单的状态或者其他出库单状态)是失败的数据行！");
                            return;
                        }
                    }
                }
                else
                {
                    this.View.ShowErrMessage("未选择数据行！");
                    return;
                }
                this.View.ShowMessage("重新获取失败的订单成功");
            }
        }
    }
}

