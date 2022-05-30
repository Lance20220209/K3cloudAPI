using AnyCubicK3cloudProject;
using Kingdee.BOS;
using Kingdee.BOS.Core.DynamicForm.PlugIn;
using Kingdee.BOS.Core.DynamicForm.PlugIn.Args;
using Kingdee.BOS.Core.Interaction;
using Kingdee.BOS.Core.List;
using Kingdee.BOS.Core.Metadata;
using Kingdee.BOS.Core.Metadata.ConvertElement.ServiceArgs;
using Kingdee.BOS.Orm;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.ServiceHelper;
using Kingdee.BOS.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;

namespace Anycubic.DC.API
{
    [Description("金蝶库存转换单生成胜途的其他出入库单")]
    [Kingdee.BOS.Util.HotUpdate]
    public class ZWLF_StockConvertAudit : AbstractOperationServicePlugIn
    {
        public override void EndOperationTransaction(EndOperationTransactionArgs e)
        {
            ZWLF_Configuration zWLF_Configuration = new ZWLF_Configuration();
            if (Context.DBId == zWLF_Configuration.DBId)
            {
                if (e.DataEntitys != null && e.DataEntitys.Count<DynamicObject>() > 0)
                {
                    foreach (DynamicObject item in e.DataEntitys)
                    {
                        long Id = Convert.ToInt64(item["Id"].ToString());
                        //配置表
                        string sql = string.Format(@"/*dialect*/ select  top 1 app_key,AppSecret,date_type,end_date,page_no,page_size,
                                     username,password,url,wh_code ,method,start_date,access_token
                                    from ZWLF_T_Configuration where  IsDisable=0");
                        //其他出库单
                        sql += string.Format(@"/*dialect*/ select FBILLNO, FENTRYID, w.FNUMBER ,FCONVERTQTY,FSTOCKID  from  T_STK_StockConvert a 
                                                       inner join T_STK_StockConvertEntry b on a.FID=b.FID 
                                                       inner join  T_BD_MATERIAL w on w.FMATERIALID=b.FMATERIALID 
                                                       where   FSTOCKID in (171002,103583,226441,171005,270325,102502,450697,450698) and FCONVERTTYPE='A' and a.F_ZWLF_PUSHSTATE!='1' and a.FID={0}", Id);
                        //其他入库单
                        sql += string.Format(@"/*dialect*/ select FBILLNO, FCONVERTENTRYID, w.FNUMBER ,FCONVERTQTY,FSTOCKID  from  T_STK_StockConvert a 
                                                       inner join T_STK_StockConvertEntry b on a.FID=b.FID 
                                                       inner join  T_BD_MATERIAL w on w.FMATERIALID=b.FMATERIALID 
                                                       where   FSTOCKID in (171002,103583,226441,171005,270325,102502,450697,450698) and FCONVERTTYPE='B' and a.F_ZWLF_PUSHSTATE!='1' and a.FID={0}", Id);
                        Parameters parameters = new Parameters();
                        DataSet ds = DBServiceHelper.ExecuteDataSet(Context, sql);
                        DataTable dt = ds.Tables[0];
                        DataTable dt_c = ds.Tables[1];
                        DataTable dt_r = ds.Tables[2];
                        if (dt.Rows.Count > 0)
                        {
                            //获取授权
                            if (dt.Rows.Count > 0)
                            {
                                for (int i = 0; i < dt.Rows.Count; i++)
                                {
                                    parameters.app_key = dt.Rows[i]["app_key"].ToString();
                                    parameters.AppSecret = dt.Rows[i]["AppSecret"].ToString();
                                    parameters.page_size = dt.Rows[i]["page_size"].ToString();
                                    parameters.username = dt.Rows[i]["username"].ToString();
                                    parameters.password = dt.Rows[i]["password"].ToString();
                                    DateTime Btime = Convert.ToDateTime(dt.Rows[i]["end_date"].ToString()).AddMinutes(-30);
                                    parameters.start_date = Btime.ToString();
                                    DateTime Etime = DateTime.Now;
                                    parameters.end_date = Etime.ToString();
                                    parameters.wh_code = dt.Rows[i]["wh_code"].ToString();
                                    parameters.page_no = Convert.ToInt32(dt.Rows[i]["page_no"].ToString());
                                    parameters.url = dt.Rows[i]["url"].ToString();
                                    parameters.method = "";
                                    parameters.id = "";
                                    //获取token
                                    parameters.access_token = dt.Rows[i]["access_token"].ToString();
                                }
                            }
                        }

                        //判断是否要生成胜途的其他出库（仓库不一致）
                        if (dt_c.Rows.Count > 0)
                        {
                            for (int i = 0; i < dt_c.Rows.Count; i++)
                            {
                                string FBILLNO = dt_c.Rows[i]["FBILLNO"].ToString();
                                //数量
                                int FCONVERTQTY = Convert.ToInt32(dt_c.Rows[i]["FCONVERTQTY"]);
                                //物料编码
                                string WlFNUMBER = dt_c.Rows[i]["FNUMBER"].ToString();
                                long FENTRYID = Convert.ToInt64(dt_c.Rows[i]["FENTRYID"].ToString());
                                //转换前的仓库
                                string FSTOCKID = dt_c.Rows[i]["FSTOCKID"].ToString();
                                //获取转换后的仓库
                                string Strsql = string.Format(@"select FSTOCKID  from  T_STK_StockConvert a inner join 
                              T_STK_StockConvertEntry b on a.FID=b.FID where FCONVERTENTRYID={0}", FENTRYID);
                                string NewFSTOCKID = DBServiceHelper.ExecuteScalar<string>(Context, Strsql, "", null);
                                //仓库不一致
                                if (FSTOCKID != NewFSTOCKID)
                                {
                                    STOtherout(parameters, WlFNUMBER, FCONVERTQTY, FSTOCKID, Id, FBILLNO);
                                }
                            }
                        }
                        //判断是否要生成胜途的其他入库（仓库不一致）
                        if (dt_r.Rows.Count > 0)
                        {
                            for (int i = 0; i < dt_r.Rows.Count; i++)
                            {
                                string FBILLNO = dt_r.Rows[i]["FBILLNO"].ToString();
                                //数量
                                int FCONVERTQTY = Convert.ToInt32(dt_r.Rows[i]["FCONVERTQTY"]);
                                //物料编码
                                string WlFNUMBER = dt_r.Rows[i]["FNUMBER"].ToString();
                                long FCONVERTENTRYID = Convert.ToInt64(dt_r.Rows[i]["FCONVERTENTRYID"].ToString());
                                //转换后
                                string FSTOCKID = dt_r.Rows[i]["FSTOCKID"].ToString();
                                //获取转换前的仓库
                                string Strsql = string.Format(@"select FSTOCKID  from  T_STK_StockConvert a inner join 
                              T_STK_StockConvertEntry b on a.FID=b.FID where FENTRYID={0}", FCONVERTENTRYID);
                                string NewFSTOCKID = DBServiceHelper.ExecuteScalar<string>(Context, Strsql, "", null);
                                //仓库不一致
                                if (FSTOCKID != NewFSTOCKID)
                                {
                                    STOtherIn(parameters, WlFNUMBER, FCONVERTQTY, FSTOCKID, Id, FBILLNO);
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 生成胜途其他入库单
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public bool STOtherIn(Parameters param, string WlFNUMBER, int FCONVERTQTY, string FSTOCKID, long Id, string FBILLNO)
        {
            bool retult = true;
            try
            {
                //应用级输入参数：
                Dictionary<string, string> @params = new Dictionary<string, string>();
                @params.Add("method", "frdas.erp.otherin.add");
                @params.Add("app_key", param.app_key);
                @params.Add("sign_method", "md5");
                // @params.Add("sign", zWLF.sign); 
                @params.Add("session", param.access_token);
                @params.Add("timestamp", DateTime.Now.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                @params.Add("format", "json");
                @params.Add("v", "1.0");
                //业务输入参数：
                //单据日期
                @params["bil_date"] = DateTime.Now.ToString();
                //单据类型
                @params["bil_type"] = "库存检验单";
                //外部单据编码
                @params["refer_no"] = FBILLNO;
                //仓库编码
                //@params["wh_code"] = "CK002";
                //仓库编码
                if (FSTOCKID == "103583")
                {
                    @params["wh_code"] = "CK002";
                }
                else if (FSTOCKID == "102502")
                {
                    @params["wh_code"] = "020";
                }
                else if (FSTOCKID == "171002")
                {
                    @params["wh_code"] = "014";
                }
                else if (FSTOCKID == "171005")
                {
                    @params["wh_code"] = "017";
                }
                else if (FSTOCKID == "226441")
                {
                    @params["wh_code"] = "019";
                }
                else if (FSTOCKID == "270325")
                {
                    @params["wh_code"] = "018";
                }
                else if (FSTOCKID == "450697")
                {
                    @params["wh_code"] = "CK029";
                }
                else if (FSTOCKID == "450698")
                {
                    @params["wh_code"] = "CK030";
                }
                //公司名称
                @params["branch_name"] = "Anycubic";//纵维接口测试;
                //备注 
                @params["memo_info"] = "金蝶的库存转换单生成胜途的其他出库单";
                //参与库龄分析 
                @params["is_stock_age"] = "True";
                //立即审核 
                @params["audit"] = "True";
                //商品信息明细
                JArray Jsonarray = new JArray();
                JObject jObject = new JObject();
                jObject.Add("prdt_code", WlFNUMBER);
                jObject.Add("qty", FCONVERTQTY);
                Jsonarray.Add(jObject);
                @params["details"] = Jsonarray.ToString();
                Httpclient httpclient = new Httpclient();
                string json = httpclient.ZWLFRequstPost(@params, param.url, param.AppSecret);
                JObject OrderJson = JsonConvert.DeserializeObject<JObject>(json);
                string code = OrderJson["code"].ToString();
                string upsql = "";
                if (code == "200")
                {
                    string bil_no = OrderJson["data"]["bil_no"].ToString();
                    upsql = string.Format(@"/*dialect*/ update T_STK_StockConvert set F_ZWLF_PUSHSTATE=1,F_ZWLF_PUSHTIME=GETDATE(),
                                                                  F_ZWLF_outNo=F_ZWLF_outNo+'{0}' where  FID='{1}'", bil_no, Id);
                    DBServiceHelper.Execute(Context, upsql);
                }
                else
                {
                    string msg = "生成胜途的其他入库单失败：" + OrderJson["msg"].ToString();
                    throw new Exception(msg);
                }

            }
            catch (KDException ex)
            {
                throw new Exception(ex.ToString());
            }
            return retult;
        }

        /// <summary>
        /// 生成胜途其他出库单
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public bool STOtherout(Parameters param, string WlFNUMBER, int FCONVERTQTY, string FSTOCKID, long Id, string FBILLNO)
        {
            bool retult = true;
            try
            {
                //应用级输入参数：
                Dictionary<string, string> @params = new Dictionary<string, string>();
                @params.Add("method", "frdas.erp.otherout.add");
                @params.Add("app_key", param.app_key);
                @params.Add("sign_method", "md5");
                // @params.Add("sign", zWLF.sign); 
                @params.Add("session", param.access_token);
                @params.Add("timestamp", DateTime.Now.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                @params.Add("format", "json");
                @params.Add("v", "1.0");
                //业务输入参数：
                //单据日期
                @params["bil_date"] = DateTime.Now.ToString();
                //单据类型
                @params["bil_type"] = "库存检验单";
                //外部单据编码
                @params["refer_no"] = FBILLNO;
                //仓库编码
                //@params["wh_code"] = "CK002";
                //仓库编码
                if (FSTOCKID == "103583")
                {
                    @params["wh_code"] = "CK002";
                }
                else if (FSTOCKID == "102502")
                {
                    @params["wh_code"] = "020";
                }
                else if (FSTOCKID == "171002")
                {
                    @params["wh_code"] = "014";
                }
                else if (FSTOCKID == "171005")
                {
                    @params["wh_code"] = "017";
                }
                else if (FSTOCKID == "226441")
                {
                    @params["wh_code"] = "019";
                }
                else if (FSTOCKID == "270325")
                {
                    @params["wh_code"] = "018";
                }
                else if (FSTOCKID == "450697")
                {
                    @params["wh_code"] = "CK029";
                }
                else if (FSTOCKID == "450698")
                {
                    @params["wh_code"] = "CK030";
                }
                //公司名称
                @params["branch_name"] = "Anycubic";//纵维接口测试;
                //备注 
                @params["memo_info"] = "金蝶的库存转换单生成胜途的其他出库单";
                //参与库龄分析 
                @params["is_stock_age"] = "True";
                //立即审核 
                @params["audit"] = "True";
                //商品信息明细
                JArray Jsonarray = new JArray();
                JObject jObject = new JObject();
                jObject.Add("prdt_code", WlFNUMBER);
                jObject.Add("qty", FCONVERTQTY);
                Jsonarray.Add(jObject);
                @params["details"] = Jsonarray.ToString();
                Httpclient httpclient = new Httpclient();
                string json = httpclient.ZWLFRequstPost(@params, param.url, param.AppSecret);
                JObject OrderJson = JsonConvert.DeserializeObject<JObject>(json);
                string code = OrderJson["code"].ToString();
                string upsql = "";
                if (code == "200")
                {
                    string bil_no = OrderJson["data"]["bil_no"].ToString();
                    upsql = string.Format(@"/*dialect*/ update T_STK_StockConvert set F_ZWLF_PUSHSTATE=1,F_ZWLF_PUSHTIME=GETDATE(),
                                                                  F_ZWLF_outNo=F_ZWLF_outNo+'{0}' where  FID='{1}'", bil_no, Id);
                    DBServiceHelper.Execute(Context, upsql);
                }
                else
                {
                    string msg = "生成胜途的其他出库单失败：" + OrderJson["msg"].ToString();
                    throw new Exception(msg);
                }
            }
            catch (KDException ex)
            {
                throw new Exception(ex.ToString());
            }
            return retult;
        }
    }
}
