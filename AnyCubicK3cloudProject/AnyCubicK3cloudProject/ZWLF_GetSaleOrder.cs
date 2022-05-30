﻿using System;
using System.Collections.Generic;
using System.Linq;
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
using System.Data;


namespace AnyCubicK3cloudProject
{
    public class ZWLF_GetSaleOrder
    {
        /// <summary>
        /// 调用胜途本地仓销售出库接口
        /// </summary>
        /// <param name="param"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public Msg SaleTradeGet(Parameters param, Context context)
        {
            Msg msg = new Msg();
            try
            {
                //应用级输入参数：
                Dictionary<string, string> @params = new Dictionary<string, string>();
                @params.Add("method", param.method);
                @params.Add("app_key", param.app_key);
                @params.Add("sign_method", "md5");
                // @params.Add("sign", zWLF.sign); 
                @params.Add("session", param.access_token);
                @params.Add("timestamp", DateTime.Now.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                @params.Add("format", "json");
                @params.Add("v", "1.0");
                //业务输入参数：
                // 默认为0（0:平台发货时间，1：系统发货时间）
                @params["date_type"] = "1";
                DateTime Btime = Convert.ToDateTime(param.start_date);
                @params["start_date"] = Btime.ToString("yyyy-MM-dd HH:mm:ss");
                DateTime Etime = Convert.ToDateTime(param.end_date);
                @params["end_date"] = Etime.ToString("yyyy-MM-dd HH:mm:ss");
                @params["page_size"] = param.page_size;
                @params["page_no"] = param.page_no.ToString();
                @params["id"] = param.id; //订单唯一标识，默认为空
                @params["site_id"] = param.site_id;// 店铺ID
                @params["wh_code"] = param.wh_code;// 仓库编码
                Httpclient httpclient = new Httpclient();
                string json = httpclient.ZWLFRequstPost(@params, param.url, param.AppSecret);
                JObject OrderJson = JsonConvert.DeserializeObject<JObject>(json);
                string code = OrderJson["code"].ToString();
                if (code == "200")
                {
                    int total_count = Convert.ToInt32(OrderJson["data"]["total_count"].ToString()); //本次返回的订单数
                    if (total_count > 0)
                    {
                        //记录每一页获取情况
                        string Insql = string.Format(@"INSERT INTO ZWLF_T_GetOrderCondition
                                                 ([Fmethod]
                                                 ,[Fdate_type]
                                                 ,[Fstart_date]
                                                 ,[Fend_date]
                                                 ,[Fpage_size]
                                                 ,[Fpage_no]
                                                 ,[FIsSuccess]
                                                 ,[FNote]
                                                  ,FStock)
                                           VALUES
                                                 ('{0}'
                                                 ,'1'
                                                 ,'{1}'
                                                 ,'{2}'
                                                 ,{3}
                                                 ,{4}
                                                 ,'1'
                                                 ,'{5}' 
                                                 ,'{6}' )", param.method, Btime, Etime, Convert.ToInt32(param.page_size), param.page_no, json.Replace("'", ""), param.wh_code);
                        DBServiceHelper.Execute(context, Insql);
                        //获取网店对应客户
                        string sql = string.Format(@"/*dialect*/ select a.FID, f.fnumber  as F_ZWLF_DEPARTMENT, c.fnumber as F_ZWLF_CUSTOMER,F_ZWLF_ONLINEACCOUNT,d.FNUMBER as F_ZWLF_SALERID,
                                                 FNAME,F_ZWLF_ISDomesticStore,F_ZWLF_STORETYPE,F_ZWLF_ORGANIZATION,c.FRECCONDITIONID  from  ZWLF_t_OnlineStore a 
                                                 inner  join ZWLF_t_OnlineStore_L  b on a.FID=b.FID 
                                                 inner join T_BD_CUSTOMER c on c.FCUSTID=a.F_ZWLF_CUSTOMER
                                                 inner join V_BD_SALESMAN d on d.fid=a.F_ZWLF_SALERID
                                                 inner  join T_BD_DEPARTMENT f on f.FDEPTID=a.F_ZWLF_DEPARTMENT
                                                 where  a.FFORBIDSTATUS='A' and a.FDOCUMENTSTATUS='C';");
                        //订单表
                        sql += string.Format(@"/*dialect*/ select FID,F_ZWLF_ID ,F_ZWLF_SITE_TRADE_ID from T_SAL_ORDER where  F_ZWLF_ID!='' and  FDATE>='2022-1-1' ;");
                        //币别
                        sql += string.Format(@"/*dialect*/ select  FNUMBER,FCODE from  T_BD_CURRENCY ;");
                        //仓库映射
                        sql += string.Format(@"/*dialect*/ select  FNUMBER,F_ZWLF_WAREHOUSECODE,FUSEORGID from  ZWLF_t_Cust_StockEntry a 
                                           inner join t_BD_Stock b on a.FStockId=b.FSTOCKID where F_ZWLF_DISABLE=0 ;");
                        //税率表
                        sql += string.Format(@"/*dialect*/  select F_ZWLF_STORETYPE ,F_ZWLF_COUNTRIES,F_ZWLF_TAXRATE from ZWLF_t_PlatformTaxRate 
                                            where  FFORBIDSTATUS='A' and FDOCUMENTSTATUS='C' ;");
                        //其他出库单
                        sql += string.Format(@"/*dialect*/ select  distinct FID,F_ZWLF_OrderNo ,F_ZWLF_ID from T_STK_MISDELIVERY where  F_ZWLF_OrderNo!='' and  FDATE>='2022-1-1'");

                        //物料
                        sql += string.Format(@"/*dialect*/ select FISINVENTORY,a.FUSEORGID ,FNUMBER ,FOLDNUMBER , FISBATCHMANAGE from T_BD_MATERIAL a
                                                          inner join t_BD_MaterialStock c on a.FMATERIALID=c.FMATERIALID
                                                         inner join t_BD_MaterialBase b  on a.FMATERIALID=b.FMATERIALID where FDOCUMENTSTATUS='C' and  FFORBIDSTATUS='A'");

                        DataSet ds = DBServiceHelper.ExecuteDataSet(context, sql);
                        //生成销售订单
                        msg = GenerateK3Order(OrderJson, ds, context, param);
                    }
                }
                else
                {
                    //记录每一页获取情况
                    string Insql = string.Format(@"INSERT INTO ZWLF_T_GetOrderCondition
                                                 ([Fmethod]
                                                 ,[Fdate_type]
                                                 ,[Fstart_date]
                                                 ,[Fend_date]
                                                 ,[Fpage_size]
                                                 ,[Fpage_no]
                                                 ,[FIsSuccess]
                                                  ,FNote
                                                  ,FStock)
                                           VALUES
                                                 ('{0}'
                                                 ,'1'
                                                 ,'{1}'
                                                 ,'{2}'
                                                 ,{3}
                                                 ,{4}
                                                 ,'0'
                                                 ,'{5}'
                                                 ,'{6}' )", param.method, param.end_date, Etime, Convert.ToInt32(param.page_size), param.page_no, OrderJson.ToString().Replace("'", ""), param.wh_code);
                    DBServiceHelper.Execute(context, Insql);
                    msg.status = false;
                }
                return msg;
            }
            catch (KDException ex)
            {
                //记录每一页获取情况
                msg.status = false;
                msg.result = ex.ToString().Substring(0, 500).Replace("'", "");
                string Insql = string.Format(@"INSERT INTO ZWLF_T_GetOrderCondition
                                                 ([Fmethod]
                                                 ,[Fdate_type]
                                                 ,[Fstart_date]
                                                 ,[Fend_date]
                                                 ,[Fpage_size]
                                                 ,[Fpage_no]
                                                 ,[FIsSuccess]
                                                 ,[FNote]
                                                 ,FStock)
                                           VALUES
                                                 ('{0}'
                                                 ,'1'
                                                 ,'{1}'
                                                 ,'{2}'
                                                 ,{3}
                                                 ,{4}
                                                 ,'0'
                                                 ,'{5}'
                                                 ,'{6}')",
                                                 param.method, param.start_date, param.end_date, Convert.ToInt32(param.page_size), param.page_no, msg.result, param.wh_code);
                DBServiceHelper.Execute(context, Insql);
                return msg;
            }
        }


        /// <summary>
        /// 生成金蝶销售订单
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        public Msg GenerateK3Order(JObject json, DataSet ds, Context context, Parameters param)
        {
            Msg msg = new Msg();
            DataTable Wdt = ds.Tables[0];
            DataTable Sodt = ds.Tables[1];
            DataTable CYdt = ds.Tables[2];
            DataTable CKdt = ds.Tables[3];
            DataTable SLdt = ds.Tables[4];
            DataTable Qtdt = ds.Tables[5];
            DataTable Wldt= ds.Tables[6];
            int total_count = Convert.ToInt32(json["data"]["total_count"].ToString()); //本次返回的订单数
            JArray array = json["data"]["data"] as JArray;
            #region 循环生成订单
            for (int i = 0; i < array.Count; i++)
            {
                string FCustId = "";
                string F_ZWLF_SALERID = "";
                string F_ZWLF_site_name = "";
                string cur_code = "";
                string FstockNumber = "";
                string F_ZWLF_DEPARTMENT = "";
                string sql = "";
                string checkMsg = "";
                bool checkstatus = true;
                decimal F_ZWLF_TAXRATE = 0; //税率
                string F_ZWLF_ISDomesticStore = ""; //是否国内网店
                string F_ZWLF_SOURCETYPE = "01";
                string FSaleOrgId = ""; //销售组织
                string FStockOrgId = "";//库存组织
                string FRECCONDITIONID = "";//收款条件
                try
                {
                    //胜途订单id
                    string F_ZWLF_id = array[i]["id"].ToString();
                    //网上订单号
                    string site_trade_id = array[i]["site_trade_id"].ToString();
                    //发货仓库编码
                    string wh_code = array[i]["wh_code"].ToString();
                    //通过网店账号和平台类型对应客户ID
                    DataRow[] WDrows = Wdt.Select("F_ZWLF_ONLINEACCOUNT='" + array[i]["site_user"].ToString() +"' and F_ZWLF_STORETYPE='"+ array[i]["site_type_id"].ToString()+"'");
                    //获取客户网店
                    if (WDrows.Length > 0)
                    {
                        foreach (DataRow dr in WDrows)
                        {
                            FCustId = dr["F_ZWLF_CUSTOMER"].ToString(); //获取对照表的客户
                            F_ZWLF_SALERID = dr["F_ZWLF_SALERID"].ToString();//获取销售员
                            F_ZWLF_site_name = dr["FID"].ToString();//获取网店
                            F_ZWLF_DEPARTMENT = dr["F_ZWLF_DEPARTMENT"].ToString();//部门
                            F_ZWLF_ISDomesticStore = dr["F_ZWLF_ISDomesticStore"].ToString();//是否国内网店
                            //销售组织
                            FSaleOrgId = dr["F_ZWLF_ORGANIZATION"].ToString();
                            FRECCONDITIONID = dr["FRECCONDITIONID"].ToString();
                        }
                    }
                    else
                    {
                        checkMsg= "找不到对应网店信息";
                        checkstatus = false;
                    }
                    //获取币别
                    DataRow[] rowCY = CYdt.Select("FCODE='" + array[i]["cur_code"].ToString() + "'");
                    if (rowCY.Length > 0)
                    {
                        foreach (DataRow dr in rowCY)
                        {
                            cur_code = dr["FNUMBER"].ToString(); //获取币别
                        }
                    }
                    else
                    {
                        checkMsg += "找不到对应币别信息";
                        checkstatus = false;
                    }
                    //获取仓库
                    DataRow[] rowCK = CKdt.Select("F_ZWLF_WAREHOUSECODE='" + array[i]["wh_code"].ToString() + "'");
                    if (rowCK.Length > 0)
                    {
                        foreach (DataRow dr in rowCK)
                        {
                            //获取仓库
                            FstockNumber = dr["FNUMBER"].ToString();
                            FStockOrgId = dr["FUSEORGID"].ToString();
                        }
                    }
                    else
                    {
                        checkMsg += "找不到对应仓库信息";
                        checkstatus = false;
                    }
                    if (param.method == "frdas.erp.saleout.getcloud")
                    {
                        F_ZWLF_SOURCETYPE = "06";
                    }
                    //国内订单的税率,其他币别为0
                    if (cur_code == "PRE001")
                    {
                        F_ZWLF_TAXRATE = 13;
                    }
                    else
                    {
                        F_ZWLF_TAXRATE = 0;
                    }
                    //只有阿里国际
                    if (array[i]["site_name"].ToString().Contains("阿里")&& array[i]["site_name"].ToString()!= "阿里德瑞")
                    {
                        //国内网店销售组织是深圳纵维
                        FSaleOrgId = "100027";
                        //销售员165157
                        F_ZWLF_SALERID = "YG202011240114";
                        //部门D05
                        F_ZWLF_DEPARTMENT = "D05";
                    }
                    if (FSaleOrgId == "165223")
                    {
                        //国外销售组织是香港纵维
                        FSaleOrgId = "165223";
                        if (F_ZWLF_TAXRATE == 0)
                        {
                            if (array[i].ToString().Contains("receiver_country"))
                            {
                                if (array[i]["receiver_country"] != null)
                                {
                                    //获取税率
                                    DataRow[] rowSL = SLdt.Select("F_ZWLF_STORETYPE='" + array[i]["site_type_id"].ToString() + "' and  F_ZWLF_COUNTRIES='" + array[i]["receiver_country"].ToString() + "'");
                                    if (rowSL.Length > 0)
                                    {
                                        foreach (DataRow dr in rowSL)
                                        {
                                            F_ZWLF_TAXRATE = Convert.ToDecimal(dr["F_ZWLF_TAXRATE"]);
                                        }
                                    }
                                }   
                            }
                        }
                    }
                    //Ebay的税率
                    if (array[i]["site_type_id"].ToString() == "102")
                    {
                        decimal Other_fee = 0;
                        decimal adjust_fee = 0;
                        decimal discount_fee = 0;
                        //其他 = adjust_fee - discount_fee
                        if (array[i].ToString().Contains("discount_fee"))
                        {
                            discount_fee = Convert.ToDecimal(array[i]["discount_fee"].ToString());
                        }
                        if (array[i].ToString().Contains("adjust_fee"))
                        {
                            adjust_fee = Convert.ToDecimal(array[i]["adjust_fee"].ToString());
                        }
                        //其他就是税额
                        Other_fee = adjust_fee - discount_fee;
                        //金额
                        decimal tatalAmtn = Convert.ToDecimal(array[i]["amtn"].ToString()) - Other_fee;
                        if (tatalAmtn > 0)
                        {
                            //税率=税额/金额
                            F_ZWLF_TAXRATE = (Other_fee / tatalAmtn) * 100;
                        }
                    }
                    //判断是否售后
                    bool isSH = false;
                    //if (array[i]["site_user"].ToString() == "009" && array[i]["site_name"].ToString() == "品牌店")
                    //{
                    //    isSH = true;
                    //}
                    if (array[i]["site_user"].ToString() == "003" && array[i]["site_name"].ToString() == "售后部")
                    {
                        isSH = true;
                    }
                    if (checkstatus)
                    {
                        //单据类型
                        string FBillTypeID = "QTCKD09_SYS";
                        //富润根据
                        if (param.method == "frdas.erp.saleout.getcloud")
                        {
                            //单据类型
                            if (array[i].ToString().Contains("bill_type"))
                            {
                                if (array[i]["bill_type"].ToString().Contains("买一赠一"))
                                {
                                    isSH = false;//生成销售订单
                                }
                                else if (array[i]["bill_type"].ToString().Contains("好评赠送"))
                                {
                                    isSH = true;
                                    FBillTypeID = "QTCKD15";//推广赠送出库单
                                }
                                else if (array[i]["bill_type"].ToString().Contains("非质量问题"))
                                {
                                    isSH = true;
                                    FBillTypeID = "QTCKD15";//推广赠送出库单
                                }
                                else if (array[i]["bill_type"].ToString().Contains("质量问题"))
                                {
                                    isSH = true;
                                    //F_ZWLF_DEPARTMENT = "D06";
                                    FBillTypeID = "QTCKD16";//售后质量赠送出库单
                                }
                                else if (array[i]["bill_type"].ToString().Contains("权重赠送"))
                                {
                                    isSH = true;
                                    FBillTypeID = "QTCKD15";//推广赠送出库单
                                }
                                else if (array[i]["bill_type"].ToString().Contains("测评赠送"))
                                {
                                    isSH = true;
                                    FBillTypeID = "QTCKD15";//推广赠送出库单
                                }
                                else if (array[i]["bill_type"].ToString().Contains("赞助赠送"))
                                {
                                    isSH = true;
                                    FBillTypeID = "QTCKD15";//推广赠送出库单
                                }
                                else if (array[i]["bill_type"].ToString().Contains("捐赠赠送"))
                                {
                                    isSH = true;
                                    FBillTypeID = "QTCKD17";//捐赠赠送
                                }
                                else if (array[i]["bill_type"].ToString().Contains("样品赠送"))
                                {
                                    isSH = true;
                                    FBillTypeID = "QTCKD18";//样品赠送
                                }
                                else if (array[i]["bill_type"].ToString().Contains("测试赠送"))
                                {
                                    isSH = true;
                                    FBillTypeID = "QTCKD19";//测试赠送
                                }
                                else if (array[i]["bill_type"].ToString().Contains("正常订单"))
                                {
                                    isSH = false;//生成销售订单
                                }
                                else
                                {
                                    FBillTypeID = "QTCKD09_SYS";//直发其他出库单
                                }
                            }
                            else
                            {
                                FBillTypeID = "QTCKD09_SYS";//直发其他出库单
                            }
                        }
                        else
                        {
                            //不需判断单据类型
                            bool istype = true;
                            //售后补发
                            if (array[i].ToString().Contains("is_resend"))
                            {
                                //售后补发
                                if (array[i]["is_resend"].ToString() == "1")
                                {
                                    istype = false;
                                    //SH-F开头都是质量问题
                                    if (site_trade_id.Contains("SH-F"))
                                    {
                                        isSH = true;
                                        //F_ZWLF_DEPARTMENT = "D06";
                                        FBillTypeID = "QTCKD16";//售后质量赠送出库单
                                    }
                                    else
                                    {
                                        if (array[i].ToString().Contains("after_sales_reason"))
                                        {
                                            if (array[i]["after_sales_reason"].ToString().Contains("买一赠一"))
                                            {
                                                isSH = false;//生成销售订单
                                            }
                                            else if (array[i]["after_sales_reason"].ToString().Contains("好评赠送"))
                                            {
                                                isSH = true;
                                                FBillTypeID = "QTCKD15";//推广赠送出库单
                                            }
                                            else if (array[i]["after_sales_reason"].ToString().Contains("非质量问题"))
                                            {
                                                isSH = true;
                                                FBillTypeID = "QTCKD15";//推广赠送出库单
                                            }
                                            else if (array[i]["after_sales_reason"].ToString().Contains("质量问题"))
                                            {
                                                isSH = true;
                                                //F_ZWLF_DEPARTMENT = "D06";
                                                FBillTypeID = "QTCKD16";//售后质量赠送出库单
                                            }
                                            else if (array[i]["after_sales_reason"].ToString().Contains("权重赠送"))
                                            {
                                                isSH = true;
                                                FBillTypeID = "QTCKD15";//推广赠送出库单
                                            }
                                            else if (array[i]["after_sales_reason"].ToString().Contains("测评赠送"))
                                            {
                                                isSH = true;
                                                FBillTypeID = "QTCKD15";//推广赠送出库单
                                            }
                                            else if (array[i]["after_sales_reason"].ToString().Contains("赞助赠送"))
                                            {
                                                isSH = true;
                                                FBillTypeID = "QTCKD15";//推广赠送出库单
                                            }
                                            else if (array[i]["after_sales_reason"].ToString().Contains("捐赠赠送"))
                                            {
                                                isSH = true;
                                                FBillTypeID = "QTCKD17";//捐赠赠送
                                            }
                                            else if (array[i]["after_sales_reason"].ToString().Contains("样品赠送"))
                                            {
                                                isSH = true;
                                                FBillTypeID = "QTCKD18";//样品赠送
                                            }
                                            else if (array[i]["after_sales_reason"].ToString().Contains("测试赠送"))
                                            {
                                                isSH = true;
                                                FBillTypeID = "QTCKD19";//测试赠送
                                            }
                                            else if (array[i]["after_sales_reason"].ToString().Contains("正常订单"))
                                            {
                                                isSH = false;//生成销售订单
                                            }
                                            else if (array[i]["after_sales_reason"].ToString().Contains("补发"))
                                            {
                                                isSH = true;
                                                FBillTypeID = "QTCKD10_SYS";//售后补发配件出库单
                                            }
                                            else
                                            {
                                                FBillTypeID = "QTCKD09_SYS";//直发其他出库单
                                            }
                                        }
                                        else
                                        {
                                            FBillTypeID = "QTCKD09_SYS";//直发其他出库单
                                        }
                                    }
                                }
                            }
                            if (istype)
                            {
                                //单据类型
                                if (array[i].ToString().Contains("reason_name"))
                                {
                                    if (array[i]["reason_name"].ToString().Contains("买一赠一"))
                                    {
                                        isSH = false;//生成销售订单
                                    }
                                    else if (array[i]["reason_name"].ToString().Contains("好评赠送"))
                                    {
                                        isSH = true;
                                        FBillTypeID = "QTCKD15";//推广赠送出库单
                                    }
                                    else if (array[i]["reason_name"].ToString().Contains("非质量问题"))
                                    {
                                        isSH = true;
                                        FBillTypeID = "QTCKD15";//推广赠送出库单
                                    }
                                    else if (array[i]["reason_name"].ToString().Contains("质量问题"))
                                    {
                                        isSH = true;
                                        //F_ZWLF_DEPARTMENT = "D06";
                                        FBillTypeID = "QTCKD16";//售后质量赠送出库单
                                    }
                                    else if (array[i]["reason_name"].ToString().Contains("权重赠送"))
                                    {
                                        isSH = true;
                                        FBillTypeID = "QTCKD15";//推广赠送出库单
                                    }
                                    else if (array[i]["reason_name"].ToString().Contains("测评赠送"))
                                    {
                                        isSH = true;
                                        FBillTypeID = "QTCKD15";//推广赠送出库单
                                    }
                                    else if (array[i]["reason_name"].ToString().Contains("赞助赠送"))
                                    {
                                        isSH = true;
                                        FBillTypeID = "QTCKD15";//推广赠送出库单
                                    }
                                    else if (array[i]["reason_name"].ToString().Contains("捐赠赠送"))
                                    {
                                        isSH = true;
                                        FBillTypeID = "QTCKD17";//捐赠赠送
                                    }
                                    else if (array[i]["reason_name"].ToString().Contains("样品赠送"))
                                    {
                                        isSH = true;
                                        FBillTypeID = "QTCKD18";//样品赠送
                                    }
                                    else if (array[i]["reason_name"].ToString().Contains("测试赠送"))
                                    {
                                        isSH = true;
                                        FBillTypeID = "QTCKD19";//测试赠送
                                    }
                                    else if(array[i]["reason_name"].ToString().Contains("正常订单"))
                                    {
                                        isSH = false;//生成销售订单
                                    }
                                    else
                                    {
                                        FBillTypeID = "QTCKD09_SYS";//直发其他出库单
                                    }
                                }
                                else
                                {
                                    //亚马逊、速卖通、Ebay、官网、LAZADA等海外平台
                                    FBillTypeID = "QTCKD09_SYS"; //直发其他出库单
                                }
                            }
                        }
                        if (FstockNumber == "CK002" || FstockNumber == "CK026" || FstockNumber == "CK016" || FstockNumber == "CK011" || FstockNumber == "CK029" || FstockNumber == "CK030")
                        {
                            //网店售后和品牌的订单不生成销售订单要走其他出库单
                            if (!isSH)
                            {
                                //校验是否已经生成销售订单
                                DataRow[] OrderRow = Sodt.Select("F_ZWLF_ID='" + F_ZWLF_id + "' and  F_ZWLF_SITE_TRADE_ID='" + site_trade_id + "'");
                                //不存在才要生成
                                if (OrderRow.Length == 0)
                                {
                                    //把前订单记录到日志里面
                                    sql = string.Format(@"/*dialect*/ INSERT INTO ZWLF_T_OrderLog
                                                      ([FID]
                                                      ,[FBILLNO]
                                                      ,[F_ZWLF_ID]
                                                      ,[F_ZWLF_SITE_TRADE_ID]
                                                      ,[F_ZWLF_TIME]
                                                      ,F_ZWLF_SOURCETYPE)
                                                    VALUES
                                                     ((select case  when max(Fid) IS NULL then '1' else max(Fid)+1 end from ZWLF_T_OrderLog)
                                                     ,(select case  when max(Fid) IS NULL then '1' else max(Fid)+1 end from ZWLF_T_OrderLog)
                                                     ,'{0}'
                                                     ,'{1}'
                                                     ,GETDATE(),'{2}')", array[i]["id"].ToString(), array[i]["site_trade_id"].ToString(), F_ZWLF_SOURCETYPE);
                                    DBServiceHelper.Execute(context, sql);

                                    #region 生成销售订单
                                    FormMetadata meta = MetaDataServiceHelper.Load(context, "SAL_SaleOrder") as FormMetadata;
                                    BusinessInfo info = meta.BusinessInfo;
                                    IResourceServiceProvider formServiceProvider = meta.BusinessInfo.GetForm().GetFormServiceProvider(true);
                                    IBillViewService billViewService = formServiceProvider.GetService(typeof(IDynamicFormView)) as IBillViewService;
                                    /******创建单据打开参数*************/
                                    Form form = meta.BusinessInfo.GetForm();
                                    BillOpenParameter billOpenParameter = new BillOpenParameter(form.Id, meta.GetLayoutInfo().Id);
                                    billOpenParameter = new BillOpenParameter(form.Id, string.Empty);
                                    billOpenParameter.Context = context;
                                    billOpenParameter.ServiceName = form.FormServiceName;
                                    billOpenParameter.PageId = Guid.NewGuid().ToString();
                                    billOpenParameter.FormMetaData = meta;
                                    billOpenParameter.LayoutId = meta.GetLayoutInfo().Id;
                                    billOpenParameter.Status = OperationStatus.ADDNEW;
                                    billOpenParameter.PkValue = null;
                                    billOpenParameter.CreateFrom = CreateFrom.Default;
                                    billOpenParameter.ParentId = 0;
                                    billOpenParameter.GroupId = "";
                                    billOpenParameter.DefaultBillTypeId = null;
                                    billOpenParameter.DefaultBusinessFlowId = null;
                                    billOpenParameter.SetCustomParameter("ShowConfirmDialogWhenChangeOrg", false);
                                    List<AbstractDynamicFormPlugIn> value = form.CreateFormPlugIns();
                                    billOpenParameter.SetCustomParameter(FormConst.PlugIns, value);
                                    ((IDynamicFormViewService)billViewService).Initialize(billOpenParameter, formServiceProvider);
                                    IBillView bill_view = (IBillView)billViewService;
                                    bill_view.CreateNewModelData();
                                    DynamicFormViewPlugInProxy proxy = bill_view.GetService<DynamicFormViewPlugInProxy>();
                                    proxy.FireOnLoad();
                                    //表头
                                    #region  表头
                                    //销售组织
                                    bill_view.Model.SetItemValueByID("FSaleOrgId", FSaleOrgId, 0);
                                    bill_view.InvokeFieldUpdateService("FSaleOrgId", 0);
                                    bill_view.Model.SetItemValueByID("FPESettleOrgId", FSaleOrgId, 0);
                                    bill_view.InvokeFieldUpdateService("FPESettleOrgId", 0);
                                    //销售员
                                    bill_view.Model.SetItemValueByNumber("FSalerId", F_ZWLF_SALERID, 0);
                                    bill_view.InvokeFieldUpdateService("FSalerId", 0);
                                    //客户
                                    bill_view.Model.SetItemValueByNumber("FCustId", FCustId, 0);
                                    bill_view.InvokeFieldUpdateService("FCustId", 0);
                                    //收款条件
                                    if (FRECCONDITIONID == "0")
                                    {
                                        bill_view.Model.SetValue("FRecConditionId", 101823, 0);
                                        bill_view.InvokeFieldUpdateService("FRecConditionId", 0);
                                    }
                                    else
                                    {
                                        bill_view.Model.SetValue("FRecConditionId", FRECCONDITIONID, 0);
                                        bill_view.InvokeFieldUpdateService("FRecConditionId", 0);
                                    }
                                    if (FRECCONDITIONID != "101823")
                                    {
                                        //是否预收
                                        bill_view.Model.SetValue("FNeedRecAdvance ", false, 0);
                                    }
                                    //单据业务日期//发货日期
                                    bill_view.Model.SetValue("FDate", array[i]["send_date"].ToString());
                                    //结算币别
                                    bill_view.Model.SetItemValueByNumber("FSettleCurrId", cur_code, 0);
                                    //bill_view.InvokeFieldUpdateService("FSettleCurrId", 0);
                                    //源单类型
                                    bill_view.Model.SetValue("F_ZWLF_SourceType", "01", 0);
                                    //税率
                                    //bill_view.Model.SetValue("FExchangeRate", 1);
                                    //所属网店 
                                    bill_view.Model.SetItemValueByID("F_ZWLF_site_name", F_ZWLF_site_name, 0);
                                    //胜途销售出库单号 
                                    bill_view.Model.SetValue("F_ZWLF_sal_bilno", array[i]["sal_bilno"].ToString(), 0);
                                    //胜途订单id
                                    bill_view.Model.SetValue("F_ZWLF_id", array[i]["id"].ToString(), 0);
                                    //网上订单号
                                    bill_view.Model.SetValue("F_ZWLF_site_trade_id", array[i]["site_trade_id"].ToString(), 0);
                                    //订单跟踪号
                                    if (array[i].ToString().Contains("logistics_no"))
                                    {
                                        bill_view.Model.SetValue("F_ZWLF_logistics_no", array[i]["logistics_no"].ToString(), 0);
                                    }
                                    else
                                    {
                                        bill_view.Model.SetValue("F_ZWLF_logistics_no", "找不到对应订单跟踪单号", 0);
                                    }
                                    //是否预收FNeedRecAdvance
                                    //bill_view.Model.SetValue("FNeedRecAdvance", false, 0);
                                    //价目表
                                    bill_view.Model.SetItemValueByID("FPriceListId", 0, 0);
                                    //收货国中文
                                    if (array[i].ToString().Contains("receiver_country_cn"))
                                    {
                                        bill_view.Model.SetValue("F_ZWLF_receiver_country_cn", array[i]["receiver_country_cn"].ToString(), 0);
                                    }
                                    if (array[i].ToString().Contains("receiver_country"))
                                    {
                                        if (array[i]["receiver_country"] != null)
                                        {
                                            bill_view.Model.SetValue("F_ZWLF_Receiver_country", array[i]["receiver_country"].ToString(), 0);
                                        }
                                    }
                                    //胜途单据类型
                                    if (array[i].ToString().Contains("reason_name"))
                                    {
                                        bill_view.Model.SetValue("F_ZWLF_ReasonName", array[i]["reason_name"].ToString(), 0);
                                    }
                                    else if (array[i].ToString().Contains("bill_type"))
                                    {
                                        bill_view.Model.SetValue("F_ZWLF_ReasonName", array[i]["bill_type"].ToString(), 0);
                                    }
                                    //合并订单号
                                    if (array[i].ToString().Contains("merge_order"))
                                    {
                                        bill_view.Model.SetValue("F_ZWLF_merge_order", array[i]["merge_order"].ToString(), 0);
                                    }
                                    //物流公司编码
                                    if (array[i].ToString().Contains("logistics_code"))
                                    {
                                        bill_view.Model.SetValue("F_ZWLF_logistics_code", array[i]["logistics_code"].ToString(), 0);
                                    }
                                    //物流公司
                                    if (array[i].ToString().Contains("logistics_name"))
                                    {
                                        bill_view.Model.SetValue("F_ZWLF_logistics_name", array[i]["logistics_name"].ToString(), 0);
                                    }
                                    //第三方出库单号
                                    if (array[i].ToString().Contains("war_out_code"))
                                    {
                                        bill_view.Model.SetValue("F_ZWLF_war_out_code", array[i]["war_out_code"].ToString(), 0);
                                    }
                                    //第三方处理编号F_ZWLF_outer_id
                                    if (array[i]["outer_id"] != null)
                                    {
                                        bill_view.Model.SetValue("F_ZWLF_outer_id", array[i]["outer_id"].ToString(), 0);
                                    }
                                    //物流包裹处理号
                                    if (array[i].ToString().Contains("logistics_outer_id"))
                                    {
                                        bill_view.Model.SetValue("F_ZWLF_logistics_outer_id", array[i]["logistics_outer_id"].ToString(), 0);
                                    }
                                    #endregion
                                    #region  表体
                                    //表体（一个订单可能又多个物料）
                                    JArray details = array[i]["details"] as JArray;
                                    for (int j = 0; j < details.Count; j++)
                                    {
                                        //国外店铺直接转换成服务物料
                                        if (FSaleOrgId == "165223")
                                        {
                                            #region 国外网店
                                            bill_view.Model.CreateNewEntryRow("FSaleOrderEntry");
                                            bill_view.Model.SetItemValueByNumber("FMaterialId", "W0006", j);
                                            bill_view.InvokeFieldUpdateService("FMATERIALID", j);
                                            bill_view.InvokeFieldUpdateService("FUNITID", j);
                                            bill_view.InvokeFieldUpdateService("FBASEUNITID", j);
                                            //税率FEntryTaxRate
                                            bill_view.Model.SetValue("FEntryTaxRate", F_ZWLF_TAXRATE, j);
                                            bill_view.InvokeFieldUpdateService("FEntryTaxRate", j);
                                            // 销售数量
                                            bill_view.Model.SetValue("FQty", details[j]["qty"].ToString(), j);
                                            bill_view.InvokeFieldUpdateService("FQty", j);
                                            bill_view.InvokeFieldUpdateService("FPriceUnitQty", j);
                                            //胜途编码
                                            bill_view.Model.SetValue("F_ZWLF_STWLBM", details[j]["prdt_code"].ToString(), j);
                                            //FIsFree 是否赠品
                                            if (details[j].Contains("is_free"))
                                            {
                                                bill_view.Model.SetValue("FIsFree", Convert.ToBoolean(details[j]["is_free"].ToString()), j);
                                            }
                                            //纸巾单价全是0
                                            if (details[j]["prdt_code"].ToString() == "M03040007")
                                            {
                                                //含税单价
                                                bill_view.Model.SetValue("FTaxPrice", 0, j);
                                            }
                                            else
                                            {
                                                //单价
                                                decimal FTaxPrice = Convert.ToDecimal(details[j]["up"].ToString());
                                                if (param.method == "frdas.erp.saleout.getcloud")
                                                {
                                                    if (details[j].ToString().Contains("amtn"))
                                                    {
                                                        //实收2022-2-7修改
                                                        decimal tatalAmtn = Convert.ToDecimal(details[j]["amtn"].ToString());
                                                        if (tatalAmtn >= 0)
                                                        {
                                                            FTaxPrice = tatalAmtn / Convert.ToDecimal(details[j]["qty"].ToString());
                                                        }
                                                    }

                                                }
                                                else
                                                {
                                                    if (details[j].ToString().Contains("real_amtn"))
                                                    {
                                                        //实收金额2022-1-2修改
                                                        decimal tatalAmtn = Convert.ToDecimal(details[j]["real_amtn"].ToString());
                                                        //if (details[j].ToString().Contains("post_fee_trade"))
                                                        //{
                                                        //    tatalAmtn = tatalAmtn + Convert.ToDecimal(details[j]["post_fee_trade"].ToString());
                                                        //}
                                                        //if (details[j].ToString().Contains("adjust_fee_trade"))
                                                        //{
                                                        //    tatalAmtn = tatalAmtn + Convert.ToDecimal(details[j]["adjust_fee_trade"].ToString());
                                                        //}
                                                        if (tatalAmtn >= 0)
                                                        {
                                                            FTaxPrice = tatalAmtn / Convert.ToDecimal(details[j]["qty"].ToString());
                                                        }
                                                    }

                                                }
                                                //含税单价
                                                bill_view.Model.SetValue("FTaxPrice", FTaxPrice, j);
                                            }
                                            bill_view.InvokeFieldUpdateService("FTaxPrice", j);
                                            //bill_view.InvokeFieldUpdateService("FPrice", j);
                                           // bill_view.InvokeFieldUpdateService("FAmount", j);
                                            //bill_view.InvokeFieldUpdateService("FAllAmount", j);
                                            //bill_view.InvokeFieldUpdateService("FAllAmount", j);
                                            //要货日期DateTime.Now.AddDays(10
                                            bill_view.Model.SetValue("FDeliveryDate", array[i]["send_date"].ToString(), j);
                                            //计划交货日期
                                            bill_view.Model.SetValue("FMinPlanDeliveryDate", array[i]["send_date"].ToString(), j);
                                            //结算组织
                                            bill_view.Model.SetItemValueByID("FSettleOrgIds", FSaleOrgId, j);
                                            //库存组织 
                                            bill_view.Model.SetItemValueByID("FStockOrgId", FStockOrgId, j);
                                            //分摊手续费
                                            if (details[j]["accounts_fee_trade"] != null)
                                            {
                                                bill_view.Model.SetValue("F_ZWLF_Poundage", details[j]["accounts_fee_trade"].ToString(), j);
                                            }
                                            #endregion
                                        }
                                        else
                                        {
                                            #region 国内销售
                                            string WLFNUMBER = details[j]["prdt_code"].ToString();
                                            string FISBATCHMANAGE = "1";
                                            //校验销售组织是否存在物料
                                            DataRow[] rowXS = Wldt.Select("FNUMBER='" + details[j]["prdt_code"].ToString() + "' and FUSEORGID='" + FSaleOrgId + "'");
                                            if (rowXS.Length == 0)
                                            {
                                                //新旧编码
                                                DataRow[] rowWL2 = Wldt.Select("FOLDNUMBER='" + details[j]["prdt_code"].ToString() + "' and FUSEORGID='" + FSaleOrgId + "'");
                                                if (rowWL2.Length == 0)
                                                {
                                                    checkMsg += "物料编码：" + details[j]["prdt_code"].ToString() + "不存在或者未审核！";
                                                    checkstatus = false;
                                                    break;
                                                }
                                            }
                                            //新旧编码
                                            DataRow[] rowWL = Wldt.Select("FNUMBER='" + details[j]["prdt_code"].ToString() + "' and FUSEORGID='" + FStockOrgId + "'");
                                            if (rowWL.Length == 0)
                                            {
                                                //新旧编码
                                                DataRow[] rowWL2 = Wldt.Select("FOLDNUMBER='" + details[j]["prdt_code"].ToString() + "' and FUSEORGID='" + FStockOrgId + "'");
                                                if (rowWL2.Length > 0)
                                                {
                                                    foreach (DataRow dr in rowWL2)
                                                    {
                                                        WLFNUMBER = dr["FNUMBER"].ToString();
                                                        FISBATCHMANAGE = dr["FISBATCHMANAGE"].ToString();
                                                    }
                                                }
                                                else //物料不存在
                                                {
                                                    checkMsg += "物料编码：" + details[j]["prdt_code"].ToString() + "不存在或者未审核！";
                                                    checkstatus = false;
                                                    break;
                                                }
                                            }
                                            else
                                            {
                                                if (rowWL.Length > 0)
                                                {
                                                    foreach (DataRow dr in rowWL)
                                                    {
                                                        FISBATCHMANAGE = dr["FISBATCHMANAGE"].ToString();
                                                    }
                                                }
                                            }
                                            //查询是否启用库存
                                            string FISINVENTORY = "0";
                                            DataRow[] row_Wl = Wldt.Select("FNUMBER='" + WLFNUMBER + "' and FUSEORGID='" + FStockOrgId + "'");
                                            if (row_Wl.Length > 0)
                                            {
                                                foreach (DataRow drN in row_Wl)
                                                {
                                                    FISINVENTORY = drN["FISINVENTORY"].ToString();
                                                }
                                            }
                                            //启用库存
                                            if (FISINVENTORY == "1")
                                            {
                                                //获取库存仓位
                                                string csql = string.Format(@"select top 1  FBASEQTY,FSTOCKLOCID,c.FNUMBER,isnull(d.FNUMBER,0) as PLFnumber from T_STK_INVENTORY  a  
                                                                      inner  join  T_BD_stock b on a.FSTOCKID=b.FSTOCKID 
                                                                      inner  join   T_BD_MATERIAL  w on w.FMATERIALID=a.FMATERIALID
                                                                      left  join T_BAS_FlexValuesDetail loc on a.FSTOCKLOCID = loc.FId
                                                                      left join t_BAS_FlexValuesEntry c on c.FENTRYID=loc.FF100004
                                                                      left join T_BD_LOTMASTER d on d.FLOTID=a.FLOT
                                                                      where w.FNUMBER='{0}' and b.FNUMBER='{1}' and FBASEQTY>0 and FSTOCKORGID='{2}' order by FBASEQTY desc ",
                                                                                      WLFNUMBER, FstockNumber, FStockOrgId);
                                                DataSet ds_loc = DBServiceHelper.ExecuteDataSet(context, csql);
                                                DataTable dt_loc = ds_loc.Tables[0];
                                                if (dt_loc.Rows.Count > 0)
                                                {
                                                    for (int b = 0; b < dt_loc.Rows.Count; b++)
                                                    {
                                                        //仓位
                                                        long FSTOCKLOCID = Convert.ToInt64(dt_loc.Rows[b]["FSTOCKLOCID"]);
                                                        //仓位编码
                                                        string FLOCFnumber = dt_loc.Rows[b]["FNUMBER"].ToString();
                                                        //批号
                                                        string PLFnumber = dt_loc.Rows[b]["PLFnumber"].ToString();
                                                        bill_view.Model.CreateNewEntryRow("FSaleOrderEntry");
                                                        bill_view.Model.SetItemValueByNumber("FMaterialId", WLFNUMBER, j);
                                                        bill_view.InvokeFieldUpdateService("FMATERIALID", j);
                                                        bill_view.InvokeFieldUpdateService("FUNITID", j);
                                                        bill_view.InvokeFieldUpdateService("FBASEUNITID", j);
                                                        //税率FEntryTaxRate
                                                        bill_view.Model.SetValue("FEntryTaxRate", F_ZWLF_TAXRATE, j);
                                                        bill_view.InvokeFieldUpdateService("FEntryTaxRate", j);
                                                        // 销售数量
                                                        bill_view.Model.SetValue("FQty", details[j]["qty"].ToString(), j);
                                                        bill_view.InvokeFieldUpdateService("FQty", j);
                                                        bill_view.InvokeFieldUpdateService("FPriceUnitQty", j);
                                                        //胜途编码
                                                        bill_view.Model.SetValue("F_ZWLF_STWLBM", details[j]["prdt_code"].ToString(), j);
                                                        //FIsFree 是否赠品
                                                        if (details[j].Contains("is_free"))
                                                        {
                                                            bill_view.Model.SetValue("FIsFree", Convert.ToBoolean(details[j]["is_free"].ToString()), j);
                                                        }
                                                        //纸巾单价全是0
                                                        if (details[j]["prdt_code"].ToString() == "M03040007")
                                                        {
                                                            //含税单价
                                                            bill_view.Model.SetValue("FTaxPrice", 0, j);
                                                        }
                                                        else
                                                        {
                                                            //单价
                                                            decimal FTaxPrice = Convert.ToDecimal(details[j]["up"].ToString());
                                                            if (param.method == "frdas.erp.saleout.getcloud")
                                                            {
                                                                if (details[j].ToString().Contains("amtn"))
                                                                {
                                                                    //实收2022-2-7修改
                                                                    decimal tatalAmtn = Convert.ToDecimal(details[j]["amtn"].ToString());
                                                                    if (tatalAmtn >= 0)
                                                                    {
                                                                        FTaxPrice = tatalAmtn / Convert.ToDecimal(details[j]["qty"].ToString());
                                                                    }
                                                                }
                                                            }
                                                            else
                                                            {
                                                                if (details[j].ToString().Contains("real_amtn"))
                                                                {
                                                                    //实收金额2022-1-2修改
                                                                    decimal tatalAmtn = Convert.ToDecimal(details[j]["real_amtn"].ToString());
                                                                    //if (details[j].ToString().Contains("post_fee_trade"))
                                                                    //{
                                                                    //    tatalAmtn = tatalAmtn + Convert.ToDecimal(details[j]["post_fee_trade"].ToString());
                                                                    //}
                                                                    //if (details[j].ToString().Contains("adjust_fee_trade"))
                                                                    //{
                                                                    //    tatalAmtn = tatalAmtn + Convert.ToDecimal(details[j]["adjust_fee_trade"].ToString());
                                                                    //}
                                                                    if (tatalAmtn >= 0)
                                                                    {
                                                                        FTaxPrice = tatalAmtn / Convert.ToDecimal(details[j]["qty"].ToString());
                                                                    }
                                                                }

                                                            }
                                                            //含税单价
                                                            bill_view.Model.SetValue("FTaxPrice", FTaxPrice, j);
                                                        }
                                                        bill_view.InvokeFieldUpdateService("FTaxPrice", j);
                                                        //bill_view.InvokeFieldUpdateService("FPrice", j);
                                                        //bill_view.InvokeFieldUpdateService("FAmount", j);
                                                        //bill_view.InvokeFieldUpdateService("FAllAmount", j);
                                                        //bill_view.InvokeFieldUpdateService("FAllAmount", j);
                                                        //库存组织 (根据实际的仓库决定)
                                                        bill_view.Model.SetItemValueByID("FStockOrgId", FStockOrgId, j);
                                                        bill_view.InvokeFieldUpdateService("FStockOrgId", j);
                                                        //仓库
                                                        bill_view.Model.SetItemValueByNumber("FSOStockId", FstockNumber, j);
                                                        bill_view.InvokeFieldUpdateService("FSOStockId", j);
                                                        //仓位
                                                        bill_view.Model.SetItemValueByID("FSOStockLocalId", FSTOCKLOCID, j);
                                                        bill_view.Model.SetItemValueByNumber("$$FSOStockLocalId__FF100001", FLOCFnumber, j);
                                                        if (FISBATCHMANAGE == "1")
                                                        {
                                                            //批号
                                                            if (PLFnumber != "0")
                                                            {
                                                                bill_view.Model.SetItemValueByNumber("FLot", PLFnumber, j);
                                                                bill_view.InvokeFieldUpdateService("FLot", j);
                                                            }
                                                        }
                                                        //要货日期DateTime.Now.AddDays(10
                                                        bill_view.Model.SetValue("FDeliveryDate", array[i]["send_date"].ToString(), j);
                                                        //计划交货日期
                                                        bill_view.Model.SetValue("FMinPlanDeliveryDate", array[i]["send_date"].ToString(), j);
                                                        //结算组织
                                                        bill_view.Model.SetItemValueByID("FSettleOrgIds", FSaleOrgId, j);
                                                        //分摊手续费
                                                        if (details[j]["accounts_fee_trade"] != null)
                                                        {
                                                            bill_view.Model.SetValue("F_ZWLF_Poundage", details[j]["accounts_fee_trade"].ToString(), j);
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    checkMsg += "物料编码：" + details[j]["prdt_code"].ToString() + "库存不足";
                                                    checkstatus = false;
                                                    break;
                                                }

                                            }
                                            else
                                            {
                                                //不启用库存的
                                                bill_view.Model.CreateNewEntryRow("FSaleOrderEntry");
                                                bill_view.Model.SetItemValueByNumber("FMaterialId", WLFNUMBER, j);
                                                bill_view.InvokeFieldUpdateService("FMATERIALID", j);
                                                bill_view.InvokeFieldUpdateService("FUNITID", j);
                                                bill_view.InvokeFieldUpdateService("FBASEUNITID", j);
                                                //税率FEntryTaxRate
                                                bill_view.Model.SetValue("FEntryTaxRate", F_ZWLF_TAXRATE, j);
                                                bill_view.InvokeFieldUpdateService("FEntryTaxRate", j);
                                                // 销售数量
                                                bill_view.Model.SetValue("FQty", details[j]["qty"].ToString(), j);
                                                bill_view.InvokeFieldUpdateService("FQty", j);
                                                bill_view.InvokeFieldUpdateService("FPriceUnitQty", j);
                                                //胜途编码
                                                bill_view.Model.SetValue("F_ZWLF_STWLBM", details[j]["prdt_code"].ToString(), j);
                                                //FIsFree 是否赠品
                                                if (details[j].Contains("is_free"))
                                                {
                                                    bill_view.Model.SetValue("FIsFree", Convert.ToBoolean(details[j]["is_free"].ToString()), j);
                                                }
                                                //纸巾单价全是0
                                                if (details[j]["prdt_code"].ToString() == "M03040007")
                                                {
                                                    //含税单价
                                                    bill_view.Model.SetValue("FTaxPrice", 0, j);
                                                }
                                                else
                                                {
                                                    //单价
                                                    decimal FTaxPrice = Convert.ToDecimal(details[j]["up"].ToString());
                                                    if (param.method == "frdas.erp.saleout.getcloud")
                                                    {
                                                        if (details[j].ToString().Contains("amtn"))
                                                        {
                                                            //实收2022-2-7修改
                                                            decimal tatalAmtn = Convert.ToDecimal(details[j]["amtn"].ToString());
                                                            if (tatalAmtn >= 0)
                                                            {
                                                                FTaxPrice = tatalAmtn / Convert.ToDecimal(details[j]["qty"].ToString());
                                                            }
                                                        }
                                                    }
                                                    else
                                                    {
                                                        if (details[j].ToString().Contains("real_amtn"))
                                                        {
                                                            //实收金额2022-1-2修改
                                                            decimal tatalAmtn = Convert.ToDecimal(details[j]["real_amtn"].ToString());
                                                            //if (details[j].ToString().Contains("post_fee_trade"))
                                                            //{
                                                            //    tatalAmtn = tatalAmtn + Convert.ToDecimal(details[j]["post_fee_trade"].ToString());
                                                            //}
                                                            //if (details[j].ToString().Contains("adjust_fee_trade"))
                                                            //{
                                                            //    tatalAmtn = tatalAmtn + Convert.ToDecimal(details[j]["adjust_fee_trade"].ToString());
                                                            //}
                                                            if (tatalAmtn >= 0)
                                                            {
                                                                FTaxPrice = tatalAmtn / Convert.ToDecimal(details[j]["qty"].ToString());
                                                            }
                                                        }

                                                    }
                                                    //含税单价
                                                    bill_view.Model.SetValue("FTaxPrice", FTaxPrice, j);
                                                }
                                                bill_view.InvokeFieldUpdateService("FTaxPrice", j);
                                                //bill_view.InvokeFieldUpdateService("FPrice", j);
                                                //bill_view.InvokeFieldUpdateService("FAmount", j);
                                                //bill_view.InvokeFieldUpdateService("FAllAmount", j);
                                                //bill_view.InvokeFieldUpdateService("FAllAmount", j);
                                                //要货日期DateTime.Now.AddDays(10
                                                bill_view.Model.SetValue("FDeliveryDate", array[i]["send_date"].ToString(), j);
                                                //计划交货日期
                                                bill_view.Model.SetValue("FMinPlanDeliveryDate", array[i]["send_date"].ToString(), j);
                                                //结算组织
                                                bill_view.Model.SetItemValueByID("FSettleOrgIds", FSaleOrgId, j);
                                                //库存组织 (根据实际的仓库决定)
                                                bill_view.Model.SetItemValueByID("FStockOrgId", FStockOrgId, j);
                                                bill_view.InvokeFieldUpdateService("FStockOrgId", j);
                                                //分摊手续费
                                                if (details[j]["accounts_fee_trade"] != null)
                                                {
                                                    bill_view.Model.SetValue("F_ZWLF_Poundage", details[j]["accounts_fee_trade"].ToString(), j);
                                                }
                                            }
                                            #endregion
                                        }
                                    }
                                    #endregion
                                    if (checkstatus)
                                    {
                                        //保存
                                        IOperationResult save_result = bill_view.Model.Save();
                                        if (save_result.IsSuccess)
                                        {
                                            string mssg = "";
                                            string fid = string.Empty;
                                            string F_ZWLF_Fbillno = string.Empty;
                                            OperateResultCollection Collection = save_result.OperateResult;
                                            foreach (var item in Collection)
                                            {
                                                fid = item.PKValue.ToString();
                                                F_ZWLF_Fbillno = item.Number.ToString();
                                            }
                                            //记录生成订单成功
                                            sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_ORDERSTATE='1',F_ZWLF_FBILLNO='{0}' 
                                                                   where F_ZWLF_ID='{1}' and F_ZWLF_site_trade_id='{2}';",
                                                                               F_ZWLF_Fbillno, array[i]["id"].ToString(), array[i]["site_trade_id"].ToString());
                                            DBServiceHelper.Execute(context, sql);
                                            try
                                            {
                                                //提交审核订单
                                                IOperationResult result = Operation(fid, meta, context);
                                                if (!result.IsSuccess)
                                                {

                                                    foreach (var item in result.ValidationErrors)
                                                    {
                                                        mssg = mssg + item.Message;
                                                    }
                                                    if (!result.InteractionContext.IsNullOrEmpty())
                                                    {
                                                        mssg = mssg + result.InteractionContext.SimpleMessage;
                                                    }
                                                    msg.result = mssg;
                                                    sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_OUTSTOCKSTATE='0',F_ZWLF_NOTE=F_ZWLF_NOTE+'{0}' 
                                                                where F_ZWLF_ID='{1}'  and F_ZWLF_site_trade_id='{2}';",
                                                                            mssg, array[i]["id"].ToString(), array[i]["site_trade_id"].ToString());
                                                }
                                                else
                                                {
                                                    mssg = "生成销售出库单成功";
                                                    msg.result = mssg;
                                                    sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_OUTSTOCKSTATE='1',F_ZWLF_NOTE='{0}' 
                                                                  where F_ZWLF_ID='{1}' and F_ZWLF_site_trade_id='{2}';",
                                                                              mssg, array[i]["id"].ToString(), array[i]["site_trade_id"].ToString());
                                                }
                                                DBServiceHelper.Execute(context, sql);
                                            }
                                            catch (KDException ex)
                                            {
                                                msg.result = ex.ToString().Substring(0, 500);
                                                sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_OUTSTOCKSTATE='0',F_ZWLF_NOTE=F_ZWLF_NOTE+'{0}'
                                                            where F_ZWLF_ID='{1}' and F_ZWLF_site_trade_id='{2}';",
                                                                        msg.result, array[i]["id"].ToString(), array[i]["site_trade_id"].ToString());
                                                DBServiceHelper.Execute(context, sql);
                                            }
                                        }
                                        else
                                        {
                                            string mssg = "";
                                            foreach (var item in save_result.ValidationErrors)
                                            {
                                                mssg = mssg + item.Message;
                                            }
                                            if (!save_result.InteractionContext.IsNullOrEmpty())
                                            {
                                                mssg = mssg + save_result.InteractionContext.SimpleMessage;
                                            }
                                            msg.result = mssg;
                                            sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_ORDERSTATE='0',F_ZWLF_NOTE=F_ZWLF_NOTE+'{0}' 
                                                                    where F_ZWLF_ID='{1}'  and F_ZWLF_site_trade_id='{2}';",
                                                                                mssg, array[i]["id"].ToString(), array[i]["site_trade_id"].ToString());
                                            DBServiceHelper.Execute(context, sql);
                                        }
                                    }
                                    else
                                    {
                                        sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_ORDERSTATE='0',F_ZWLF_NOTE=F_ZWLF_NOTE+'{0}' 
                                                                  where F_ZWLF_ID='{1}' and F_ZWLF_site_trade_id='{2}';",
                                                                             checkMsg, array[i]["id"].ToString(), array[i]["site_trade_id"].ToString());
                                        DBServiceHelper.Execute(context, sql);
                                    }
                                    #endregion

                                }
                            }
                            //国外网店 售后网店走其他出库
                            if (FSaleOrgId == "165223" || isSH)
                            {
                                if (FstockNumber == "CK002" || FstockNumber == "CK016" || FstockNumber == "CK026" || FstockNumber == "CK011" || FstockNumber == "CK029" || FstockNumber == "CK030")
                                {
                                    //校验是否已经其他出库单
                                    DataRow[] QtRow = Qtdt.Select("F_ZWLF_ID='" + F_ZWLF_id + "' and  F_ZWLF_OrderNo='" + site_trade_id + "'");
                                    if (QtRow.Length == 0)
                                    {
                                        //把前订单记录到日志里面
                                        sql = string.Format(@"/*dialect*/ INSERT INTO ZWLF_T_OrderLog
                                                      ([FID]
                                                      ,[FBILLNO]
                                                      ,[F_ZWLF_ID]
                                                      ,[F_ZWLF_SITE_TRADE_ID]
                                                      ,[F_ZWLF_TIME]
                                                      ,F_ZWLF_SOURCETYPE)
                                                    VALUES
                                                     ((select case  when max(Fid) IS NULL then '1' else max(Fid)+1 end from ZWLF_T_OrderLog)
                                                     ,(select case  when max(Fid) IS NULL then '1' else max(Fid)+1 end from ZWLF_T_OrderLog)
                                                     ,'{0}'
                                                     ,'{1}'
                                                     ,GETDATE(),'{2}')", array[i]["id"].ToString(), array[i]["site_trade_id"].ToString(), F_ZWLF_SOURCETYPE);
                                        DBServiceHelper.Execute(context, sql);
                                        #region 生成深圳纵维或者惠州纵维的其他出库
                                        FormMetadata meta = MetaDataServiceHelper.Load(context, "STK_MisDelivery") as FormMetadata;
                                        BusinessInfo info = meta.BusinessInfo;
                                        IResourceServiceProvider formServiceProvider = meta.BusinessInfo.GetForm().GetFormServiceProvider(true);
                                        IBillViewService billViewService = formServiceProvider.GetService(typeof(IDynamicFormView)) as IBillViewService;
                                        /******创建单据打开参数*************/
                                        Form form = meta.BusinessInfo.GetForm();
                                        BillOpenParameter billOpenParameter = new BillOpenParameter(form.Id, meta.GetLayoutInfo().Id);
                                        billOpenParameter = new BillOpenParameter(form.Id, string.Empty);
                                        billOpenParameter.Context = context;
                                        billOpenParameter.ServiceName = form.FormServiceName;
                                        billOpenParameter.PageId = Guid.NewGuid().ToString();
                                        billOpenParameter.FormMetaData = meta;
                                        billOpenParameter.LayoutId = meta.GetLayoutInfo().Id;
                                        billOpenParameter.Status = OperationStatus.ADDNEW;
                                        billOpenParameter.PkValue = null;
                                        billOpenParameter.CreateFrom = CreateFrom.Default;
                                        billOpenParameter.ParentId = 0;
                                        billOpenParameter.GroupId = "";
                                        billOpenParameter.DefaultBillTypeId = null;
                                        billOpenParameter.DefaultBusinessFlowId = null;
                                        billOpenParameter.SetCustomParameter("ShowConfirmDialogWhenChangeOrg", false);
                                        List<AbstractDynamicFormPlugIn> value = form.CreateFormPlugIns();
                                        billOpenParameter.SetCustomParameter(FormConst.PlugIns, value);
                                        ((IDynamicFormViewService)billViewService).Initialize(billOpenParameter, formServiceProvider);
                                        IBillView bill_view = (IBillView)billViewService;
                                        bill_view.CreateNewModelData();
                                        DynamicFormViewPlugInProxy proxy = bill_view.GetService<DynamicFormViewPlugInProxy>();
                                        proxy.FireOnLoad();
                                        //表头
                                        #region  表头
                                        bill_view.Model.SetItemValueByID("FStockOrgId", FStockOrgId, 0);//组织
                                        bill_view.Model.SetItemValueByID("FPickOrgId", FStockOrgId, 0);
                                        bill_view.Model.SetItemValueByID("FOwnerIdHead", FStockOrgId, 0);
                                        //单据类型 
                                        bill_view.Model.SetItemValueByNumber("FBillTypeID", FBillTypeID, 0);
                                        //单据业务日期
                                        bill_view.Model.SetValue("FDate", array[i]["send_date"].ToString());
                                        //领料部门
                                        bill_view.Model.SetItemValueByNumber("FDeptId", F_ZWLF_DEPARTMENT, 0);
                                        bill_view.InvokeFieldUpdateService("FDeptId", 0);
                                        //表头备注
                                        bill_view.Model.SetValue("FNote", "胜途销售订单生成金蝶其他出库单" + array[i]["site_trade_id"].ToString(), 0);
                                        //备注
                                        bill_view.Model.SetValue("F_ZWLF_Note", "胜途销售订单生成金蝶其他出库单", 0);
                                        //胜途销售订单号F_ZWLF_OrderNo
                                        bill_view.Model.SetValue("F_ZWLF_OrderNo", array[i]["site_trade_id"].ToString(), 0);
                                        //胜途订单唯一标识 
                                        bill_view.Model.SetValue("F_ZWLF_ID", F_ZWLF_id, 0);
                                        //标记推送状态
                                        bill_view.Model.SetValue("F_ZWLF_PushState", 1, 0);
                                        //胜途单据类型
                                        if (array[i].ToString().Contains("reason_name"))
                                        {
                                            bill_view.Model.SetValue("F_ZWLF_ReasonName", array[i]["reason_name"].ToString(), 0);
                                        }
                                        else if (array[i].ToString().Contains("bill_type"))
                                        {
                                            bill_view.Model.SetValue("F_ZWLF_ReasonName", array[i]["bill_type"].ToString(), 0);
                                        }
                                        //合并订单号
                                        if (array[i].ToString().Contains("merge_order"))
                                        {
                                            bill_view.Model.SetValue("F_ZWLF_merge_order", array[i]["merge_order"].ToString(), 0);
                                        }
                                        #endregion
                                        #region  表体
                                        //只要有一个物料没有库存就不生成
                                        bool ISQT = true;
                                        string mssQt = "生成其他出库单失败：";
                                        //表体（一个订单可能又多个物料）
                                        JArray details = array[i]["details"] as JArray;
                                        for (int j = 0; j < details.Count; j++)
                                        {
                                            string WLFNUMBER = details[j]["prdt_code"].ToString();
                                            string FISBATCHMANAGE = "1";
                                            //新旧编码
                                            DataRow[] rowWL = Wldt.Select("FNUMBER='" + details[j]["prdt_code"].ToString() + "' and FUSEORGID='" + FStockOrgId + "'");
                                            if (rowWL.Length == 0)
                                            {
                                                //新旧编码
                                                DataRow[] rowWL2 = Wldt.Select("FOLDNUMBER='" + details[j]["prdt_code"].ToString() + "' and FUSEORGID='" + FStockOrgId + "'");
                                                if (rowWL2.Length > 0)
                                                {
                                                    foreach (DataRow dr in rowWL2)
                                                    {
                                                        WLFNUMBER = dr["FNUMBER"].ToString();
                                                        FISBATCHMANAGE = dr["FISBATCHMANAGE"].ToString();
                                                    }
                                                }
                                                else
                                                {
                                                    ISQT = false;
                                                    //记录生成订单失败
                                                    mssQt += "物料编码为：" + details[j]["prdt_code"].ToString() + "不存在或者未分配到对应组织";
                                                    break;
                                                }
                                            }
                                            else
                                            {
                                                if (rowWL.Length > 0)
                                                {
                                                    foreach (DataRow dr in rowWL)
                                                    {
                                                        FISBATCHMANAGE = dr["FISBATCHMANAGE"].ToString();
                                                    }
                                                }
                                            }
                                            //查询是否启用库存
                                            string FISINVENTORY = "0";
                                            DataRow[] row_Wl = Wldt.Select("FNUMBER='" + WLFNUMBER + "' and FUSEORGID='" + FStockOrgId + "'");
                                            if (row_Wl.Length > 0)
                                            {
                                                foreach (DataRow drN in row_Wl)
                                                {
                                                    FISINVENTORY = drN["FISINVENTORY"].ToString();
                                                }
                                            }
                                            if (FISINVENTORY == "1")
                                            {
                                                //获取库存仓位
                                                string csql = string.Format(@"select top 1  FBASEQTY,FSTOCKLOCID,c.FNUMBER,isnull(d.FNUMBER,0) as PLFnumber from T_STK_INVENTORY  a  
                                                                      inner  join  T_BD_stock b on a.FSTOCKID=b.FSTOCKID 
                                                                      inner  join   T_BD_MATERIAL  w on w.FMATERIALID=a.FMATERIALID
                                                                      left  join T_BAS_FlexValuesDetail loc on a.FSTOCKLOCID = loc.FId
                                                                      left join t_BAS_FlexValuesEntry c on c.FENTRYID=loc.FF100004
                                                                      left join T_BD_LOTMASTER d on d.FLOTID=a.FLOT
                                                                      where w.FNUMBER='{0}' and b.FNUMBER='{1}' and FBASEQTY>0 and FSTOCKORGID='{2}' order by FBASEQTY desc ",
                                                                                     WLFNUMBER, FstockNumber, FStockOrgId);
                                                DataSet ds_loc = DBServiceHelper.ExecuteDataSet(context, csql);
                                                DataTable dt_loc = ds_loc.Tables[0];
                                                if (dt_loc.Rows.Count > 0)
                                                {
                                                    for (int b = 0; b < dt_loc.Rows.Count; b++)
                                                    {
                                                        //当前库存数
                                                        double FBASEQTY = Convert.ToDouble(dt_loc.Rows[b]["FBASEQTY"]);
                                                        //仓位
                                                        long FSTOCKLOCID = Convert.ToInt64(dt_loc.Rows[b]["FSTOCKLOCID"]);
                                                        //仓位编码
                                                        string FLOCFnumber = dt_loc.Rows[b]["FNUMBER"].ToString();
                                                        //批号
                                                        string PLFnumber = dt_loc.Rows[b]["PLFnumber"].ToString();
                                                        bill_view.Model.CreateNewEntryRow("FEntity");
                                                        bill_view.Model.SetItemValueByNumber("FMaterialId", WLFNUMBER, j);
                                                        bill_view.InvokeFieldUpdateService("FMaterialId", j);
                                                        bill_view.InvokeFieldUpdateService("FUnitID", j);
                                                        bill_view.InvokeFieldUpdateService("FBaseUnitId", j);
                                                        //实发数量
                                                        bill_view.Model.SetValue("FQty", details[j]["qty"].ToString(), j);
                                                        bill_view.InvokeFieldUpdateService("FQty", j);
                                                        bill_view.InvokeFieldUpdateService("FBaseQty", j);
                                                        //源单编号
                                                        bill_view.Model.SetValue("FSrcBillNo", array[i]["site_trade_id"].ToString(), j);
                                                        //仓库111252
                                                        bill_view.Model.SetItemValueByNumber("FStockId", FstockNumber, j);
                                                        bill_view.InvokeFieldUpdateService("FStockId", j);
                                                        //仓位
                                                        bill_view.Model.SetItemValueByID("FStockLocId", FSTOCKLOCID, j);
                                                        bill_view.Model.SetItemValueByNumber("$$FStockLocId__FF100001", FLOCFnumber, j);
                                                        if (FISBATCHMANAGE == "1")
                                                        {
                                                            //批号
                                                            if (PLFnumber != "0")
                                                            {
                                                                bill_view.Model.SetItemValueByNumber("FLot", PLFnumber, j);
                                                                bill_view.InvokeFieldUpdateService("FLot", j);
                                                            }
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    ISQT = false;
                                                    //记录生成订单失败
                                                    mssQt += "物料编码为：" + details[j]["prdt_code"].ToString() + "没有库存";
                                                    break;
                                                }
                                            }
                                            else
                                            {
                                                //不启用库存
                                                bill_view.Model.CreateNewEntryRow("FEntity");
                                                bill_view.Model.SetItemValueByNumber("FMaterialId", WLFNUMBER, j);
                                                bill_view.InvokeFieldUpdateService("FMaterialId", j);
                                                bill_view.InvokeFieldUpdateService("FUnitID", j);
                                                bill_view.InvokeFieldUpdateService("FBaseUnitId", j);
                                                //实发数量
                                                bill_view.Model.SetValue("FQty", details[j]["qty"].ToString(), j);
                                                bill_view.InvokeFieldUpdateService("FQty", j);
                                                bill_view.InvokeFieldUpdateService("FBaseQty", j);
                                                //源单编号
                                                bill_view.Model.SetValue("FSrcBillNo", array[i]["site_trade_id"].ToString(), j);
                                                //仓库111252
                                                bill_view.Model.SetItemValueByNumber("FStockId", FstockNumber, j);
                                                bill_view.InvokeFieldUpdateService("FStockId", j);
                                            }
                                        }
                                        #endregion
                                        //校验是否一个订单里面某个物料没有库存
                                        if (ISQT)
                                        {
                                            //保存
                                            IOperationResult save_result = bill_view.Model.Save();
                                            if (save_result.IsSuccess)
                                            {
                                                string mssg = "";
                                                string fid = string.Empty;
                                                string F_ZWLF_Fbillno = string.Empty;
                                                OperateResultCollection Collection = save_result.OperateResult;
                                                foreach (var item in Collection)
                                                {
                                                    fid = item.PKValue.ToString();
                                                    F_ZWLF_Fbillno = item.Number.ToString();
                                                }
                                                //记录生成订单成功
                                                sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_STKState='1',F_ZWLF_QTFbillno='{0}' 
                                                            where F_ZWLF_ID='{1}' and F_ZWLF_site_trade_id='{2}';",
                                                                        F_ZWLF_Fbillno, array[i]["id"].ToString(), array[i]["site_trade_id"].ToString());
                                                DBServiceHelper.Execute(context, sql);
                                                try
                                                {
                                                    //提交审核订单
                                                    IOperationResult result = Operation(fid, meta, context);
                                                    if (!result.IsSuccess)
                                                    {
                                                        foreach (var item in result.ValidationErrors)
                                                        {
                                                            mssg = mssg + item.Message;
                                                        }
                                                        if (!result.InteractionContext.IsNullOrEmpty())
                                                        {
                                                            mssg = mssg + result.InteractionContext.SimpleMessage;
                                                        }
                                                        msg.result = mssg;
                                                        sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_STKState='0',F_ZWLF_NOTE=F_ZWLF_NOTE+'{0}' 
                                                                  where F_ZWLF_ID='{1}' and F_ZWLF_site_trade_id='{2}' ;",
                                                                              mssg, array[i]["id"].ToString(), array[i]["site_trade_id"].ToString());
                                                    }
                                                    else
                                                    {
                                                        mssg = "生成其他出库单成功";
                                                        msg.result = mssg;
                                                        sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_STKState='1',F_ZWLF_NOTE=F_ZWLF_NOTE+'{0}' 
                                                                where F_ZWLF_ID='{1}'  and F_ZWLF_site_trade_id='{2}';",
                                                                            mssg, array[i]["id"].ToString(), array[i]["site_trade_id"].ToString());
                                                    }
                                                    DBServiceHelper.Execute(context, sql);

                                                }
                                                catch (KDException ex)
                                                {
                                                    msg.result = ex.ToString().Substring(0, 500);
                                                    sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_STKState='0',F_ZWLF_NOTE=F_ZWLF_NOTE+'{0}' 
                                                                          where F_ZWLF_ID='{1}' and F_ZWLF_site_trade_id='{2}';",
                                                                                      msg.result, array[i]["id"].ToString(), array[i]["site_trade_id"].ToString());
                                                    DBServiceHelper.Execute(context, sql);
                                                }
                                            }
                                            else
                                            {
                                                string mssg = "";
                                                foreach (var item in save_result.ValidationErrors)
                                                {
                                                    mssg = mssg + item.Message;
                                                }
                                                if (!save_result.InteractionContext.IsNullOrEmpty())
                                                {
                                                    mssg = mssg + save_result.InteractionContext.SimpleMessage;
                                                }
                                                msg.result = mssg;
                                                sql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_STKState='0',F_ZWLF_NOTE=F_ZWLF_NOTE+'{0}'
                                                            where F_ZWLF_ID='{1}'  and F_ZWLF_site_trade_id='{2}';",
                                                                        mssg, array[i]["id"].ToString(), array[i]["site_trade_id"].ToString());
                                                DBServiceHelper.Execute(context, sql);
                                            }

                                        }
                                        else //记录没有库存的物料
                                        {
                                            //记录生成订单失败
                                            string Insql = string.Format(@"/*dialect*/ update ZWLF_T_OrderLog set F_ZWLF_STKState='0',F_ZWLF_NOTE=F_ZWLF_NOTE+'{0}' 
                                                                   where F_ZWLF_ID='{1}' and F_ZWLF_site_trade_id='{2}';",
                                                                              mssQt, array[i]["id"].ToString(),
                                                                              array[i]["site_trade_id"].ToString());
                                            DBServiceHelper.Execute(context, Insql);
                                        }
                                        #endregion
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        //把前订单记录到日志里面
                        sql = string.Format(@"/*dialect*/ INSERT INTO ZWLF_T_OrderLog
                                                      ([FID]
                                                      ,[FBILLNO]
                                                      ,[F_ZWLF_ID]
                                                      ,[F_ZWLF_SITE_TRADE_ID]
                                                      ,[F_ZWLF_TIME]
                                                      ,F_ZWLF_SOURCETYPE,F_ZWLF_ORDERSTATE,F_ZWLF_NOTE)
                                                    VALUES
                                                     ((select case  when max(Fid) IS NULL then '1' else max(Fid)+1 end from ZWLF_T_OrderLog)
                                                     ,(select case  when max(Fid) IS NULL then '1' else max(Fid)+1 end from ZWLF_T_OrderLog)
                                                     ,'{0}'
                                                     ,'{1}'
                                                     ,GETDATE(),'{2}','0','{3}')", array[i]["id"].ToString(), array[i]["site_trade_id"].ToString(), F_ZWLF_SOURCETYPE, checkMsg);
                        DBServiceHelper.Execute(context, sql);
                    }
                }
                catch (KDException ex)
                {
                    //记录生成订单失败
                    msg.result = ex.ToString().Substring(0, 500);
                    //把前订单记录到日志里面
                    sql = string.Format(@"/*dialect*/ INSERT INTO ZWLF_T_OrderLog
                                                      ([FID]
                                                      ,[FBILLNO]
                                                      ,[F_ZWLF_ID]
                                                      ,[F_ZWLF_SITE_TRADE_ID]
                                                      ,[F_ZWLF_TIME]
                                                      ,F_ZWLF_SOURCETYPE,F_ZWLF_ORDERSTATE,F_ZWLF_NOTE)
                                                    VALUES
                                                     ((select case  when max(Fid) IS NULL then '1' else max(Fid)+1 end from ZWLF_T_OrderLog)
                                                     ,(select case  when max(Fid) IS NULL then '1' else max(Fid)+1 end from ZWLF_T_OrderLog)
                                                     ,'{0}'
                                                     ,'{1}'
                                                     ,GETDATE(),'{2}','0','{3}')", array[i]["id"].ToString(), array[i]["site_trade_id"].ToString(), F_ZWLF_SOURCETYPE, msg.result);
                    DBServiceHelper.Execute(context, sql);
                }
            }
            #endregion
            msg.status = true;
            msg.sum = array.Count;
            return msg;
        }

        /// <summary>
        /// 提交审核
        /// </summary>
        /// <param name="FID"></param>
        /// <param name="materialmeta"></param>
        /// <returns></returns>
        private IOperationResult Operation(string FID, FormMetadata materialmeta, Context context)
        {
            OperateOption option = OperateOption.Create();
            option.SetIgnoreWarning(false);
            option.SetVariableValue("ignoreTransaction", false);
            IOperationResult result = null;
            //提交
            object[] items = { FID };
            ISubmitService submitService = Kingdee.BOS.App.ServiceHelper.GetService<ISubmitService>();
            result = submitService.Submit(context, materialmeta.BusinessInfo, items, "Submit", option);
            //审核
            IAuditService auditService = Kingdee.BOS.App.ServiceHelper.GetService<IAuditService>();
            result = auditService.Audit(context, materialmeta.BusinessInfo, items, option);
            return result;
        }
    }
}
