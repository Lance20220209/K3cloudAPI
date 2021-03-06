using AnyCubicK3cloudProject;
using Kingdee.BOS;
using Kingdee.BOS.Contracts;
using Kingdee.BOS.Core.DynamicForm;
using Kingdee.BOS.Core.DynamicForm.Operation;
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
    [Description("直接调拨单审核推送胜途")]
    [Kingdee.BOS.Util.HotUpdate]
    public class ZWLF_TransferDirect : AbstractOperationServicePlugIn
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
                        long FID = Convert.ToInt64(item["Id"].ToString());
                        //配置表
                        string sql = string.Format(@"/*dialect*/ select  top 1 app_key,AppSecret,date_type,end_date,page_no,page_size,
                                     username,password,url,wh_code ,method,start_date,access_token
                                    from ZWLF_T_Configuration where  IsDisable=0");
                        //对应胜途采购入库单
                        sql += string.Format(@"/*dialect*/select FBILLNO, FDATE,FSTOCKORGID,b.FDESTSTOCKID as FSTOCKID,a.FAPPROVEDATE ,wl.FNUMBER, FQTY, a.FNOTE from  T_STK_STKTRANSFERIN a 
                                                       inner join T_STK_STKTRANSFERINENTRY b on a.FID=b.Fid
                                                       join  t_BD_Stock ck on ck.FSTOCKID=b.FDESTSTOCKID
                                                       join T_BD_MATERIAL wl on wl.FMATERIALID=b.FMATERIALID
                                                       where a.F_ZWLF_PUSHSTATE!='1'and  b.FDESTSTOCKID in (171002,103583,226441,171005,270325,102502,450697,450698)
                                                       and a.FSTOCKORGID in(100027,165222)  and a.FID={0} and  b.FSRCSTOCKID!=b.FDESTSTOCKID
                                                       ", FID);
                        //对应胜途其他出库单
                        sql += string.Format(@"/*dialect*/select FBILLNO, FDATE,FSTOCKORGID,b.FSRCSTOCKID as FSTOCKID ,a.FAPPROVEDATE ,wl.FNUMBER, FQTY, a.FNOTE from  T_STK_STKTRANSFERIN a 
                                                       inner join T_STK_STKTRANSFERINENTRY b on a.FID=b.Fid
                                                       join  t_BD_Stock ck on ck.FSTOCKID=b.FSRCSTOCKID
                                                       join T_BD_MATERIAL wl on wl.FMATERIALID=b.FMATERIALID
                                                       where a.F_ZWLF_PUSHSTATE!='1'and  b.FSRCSTOCKID in (171002,103583,226441,171005,270325,102502,450697,450698)
                                                       and a.FSTOCKORGID in(100027,165222)  and a.FID={0} and  b.FSRCSTOCKID!=b.FDESTSTOCKID
                                                       ", FID);
                        DataSet ds = DBServiceHelper.ExecuteDataSet(Context, sql);
                        DataTable dt = ds.Tables[0];
                        DataTable dt1 = ds.Tables[1];
                        DataTable dt2 = ds.Tables[2];
                        if (dt.Rows.Count > 0)
                        {
                            //获取授权
                            if (dt.Rows.Count > 0)
                            {
                                for (int i = 0; i < dt.Rows.Count; i++)
                                {
                                    Parameters parameters = new Parameters();
                                    parameters.app_key = dt.Rows[i]["app_key"].ToString();
                                    parameters.AppSecret = dt.Rows[i]["AppSecret"].ToString();
                                    parameters.page_size = dt.Rows[i]["page_size"].ToString();
                                    parameters.username = dt.Rows[i]["username"].ToString();
                                    parameters.password = dt.Rows[i]["password"].ToString();
                                    parameters.url = dt.Rows[i]["url"].ToString();
                                    //获取token
                                    parameters.access_token = dt.Rows[i]["access_token"].ToString();
                                    bool result = true;
                                    //其他出库单
                                    if (dt2.Rows.Count > 0)
                                    {
                                        result = STOtherOut(parameters, dt2);
                                    }
                                    if (result)
                                    {
                                        //采购入库单
                                        if (dt1.Rows.Count > 0)
                                        {
                                            result = STOtherIn(parameters, dt1);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        /// <summary>
        /// 新增采购入库单
        /// </summary>
        /// <param name="param"></param>
        /// <param name="ds"></param>
        /// <returns></returns>
        public bool STOtherIn(Parameters param, DataTable dt)
        {
            bool result = true;
            try
            {
                //应用级输入参数：
                Dictionary<string, string> @params = new Dictionary<string, string>();
                @params.Add("method", "frdas.erp.purin.add");
                @params.Add("app_key", param.app_key);
                @params.Add("sign_method", "md5");
                // @params.Add("sign", zWLF.sign); 
                @params.Add("session", param.access_token);
                @params.Add("timestamp", DateTime.Now.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                @params.Add("format", "json");
                @params.Add("v", "1.0");
                //外部单号
                @params.Add("refer_no", dt.Rows[0]["FBILLNO"].ToString());
                //单据日期
                @params.Add("bil_date", dt.Rows[0]["FDate"].ToString());
                //仓库编码
                if (dt.Rows[0]["FSTOCKID"].ToString() == "103583")
                {
                    @params["wh_code"] = "CK002";
                }
                else if (dt.Rows[0]["FSTOCKID"].ToString() == "102502")
                {
                    @params["wh_code"] = "020";
                }
                else if (dt.Rows[0]["FSTOCKID"].ToString() == "171002")
                {
                    @params["wh_code"] = "014";
                }
                else if (dt.Rows[0]["FSTOCKID"].ToString() == "171005")
                {
                    @params["wh_code"] = "017";
                }
                else if (dt.Rows[0]["FSTOCKID"].ToString() == "226441")
                {
                    @params["wh_code"] = "019";
                }
                else if (dt.Rows[0]["FSTOCKID"].ToString() == "270325")
                {
                    @params["wh_code"] = "018";
                }
                else if (dt.Rows[0]["FSTOCKID"].ToString() == "450697")
                {
                    @params["wh_code"] = "CK029";
                }
                else if (dt.Rows[0]["FSTOCKID"].ToString() == "450698")
                {
                    @params["wh_code"] = "CK030";
                }
                //供应商
                @params.Add("cust_name", "Anycubic");
                //币种
                @params.Add("cur_code", "CNY");
                //制单人
                @params.Add("user_name", "超级管理员");
                //审核日期
                @params.Add("chk_date", DateTime.Now.ToString("yyyy-MM-dd HH: mm:ss"));
                //审核日期
                @params.Add("chk_user_name", "超级管理员");
                //公司名称
                @params["branch_name"] = "Anycubic";
                //备注 
                @params["memo_info"] = "金蝶的直接调拨："+ dt.Rows[0]["FBILLNO"].ToString() + "单生成胜途的采购入库单";
                //参与库龄分析 
                @params["is_stock_age"] = "True";
                //部门编码 
                @params["dept_code"] = "D1002";
                //商品信息明细
                JArray Jsonarray = new JArray();
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    JObject jObject = new JObject();
                    jObject.Add("prdt_code", dt.Rows[i]["FNUMBER"].ToString());
                    jObject.Add("qty", Convert.ToInt32(dt.Rows[i]["FQTY"]));
                    jObject.Add("tax_rto", 0);
                    jObject.Add("up_tax", 1);
                    Jsonarray.Add(jObject);
                }
                @params["details"] = Jsonarray.ToString();
                Httpclient httpclient = new Httpclient();
                string json = httpclient.ZWLFRequstPost(@params, param.url, param.AppSecret);
                JObject OrderJson = JsonConvert.DeserializeObject<JObject>(json);
                string code = OrderJson["code"].ToString();
                string upsql = "";
                if (code == "200")
                {
                    string bil_no = OrderJson["data"]["bil_no"].ToString();
                    upsql = string.Format(@"/*dialect*/ update T_STK_STKTRANSFERIN set F_ZWLF_PUSHSTATE=1,F_ZWLF_PUSHTIME=GETDATE(),
                                                                  F_ZWLF_INNO='{0}' where  FBILLNO='{1}'", bil_no, dt.Rows[0]["FBILLNO"].ToString());
                    result = true; 
                }
                else
                {
                    string msg = "生成胜途的采购入库单失败：" + OrderJson["msg"].ToString();
                    throw new Exception(msg);
                }
                DBServiceHelper.Execute(Context, upsql);
            }
            catch (KDException ex)
            {
                result = false;
                throw new Exception(ex.ToString());

            }
            return result;
        }



        /// <summary>
        /// 生成胜途其他出入库单据
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public bool STOtherOut(Parameters param, DataTable dt)
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
                @params["bil_date"] = dt.Rows[0]["FDATE"].ToString();
                //单据类型
                @params["bil_type"] = "调拨出库";
                //外部单据编码
                @params["refer_no"] = dt.Rows[0]["FBILLNO"].ToString();
                //仓库编码
                //@params["wh_code"] = "CK002";
                //仓库编码
                if (dt.Rows[0]["FSTOCKID"].ToString() == "103583")
                {
                    @params["wh_code"] = "CK002";
                }
                else if (dt.Rows[0]["FSTOCKID"].ToString() == "102502")
                {
                    @params["wh_code"] = "020";
                }
                else if (dt.Rows[0]["FSTOCKID"].ToString() == "171002")
                {
                    @params["wh_code"] = "014";
                }
                else if (dt.Rows[0]["FSTOCKID"].ToString() == "171005")
                {
                    @params["wh_code"] = "017";
                }
                else if (dt.Rows[0]["FSTOCKID"].ToString() == "226441")
                {
                    @params["wh_code"] = "019";
                }
                else if (dt.Rows[0]["FSTOCKID"].ToString() == "270325")
                {
                    @params["wh_code"] = "018";
                }
                else if (dt.Rows[0]["FSTOCKID"].ToString() == "450697")
                {
                    @params["wh_code"] = "CK029";
                }
                else if (dt.Rows[0]["FSTOCKID"].ToString() == "450698")
                {
                    @params["wh_code"] = "CK030";
                }
                //公司名称
                @params["branch_name"] = "Anycubic";//纵维接口测试;
                //备注 
                @params["memo_info"] = "金蝶的直接调拨：" + dt.Rows[0]["FBILLNO"].ToString() + "单生成胜途的其他出库单";
                //参与库龄分析 
                @params["is_stock_age"] = "True";
                //立即审核 
                @params["audit"] = "True";
                //商品信息明细
                JArray Jsonarray = new JArray();
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    JObject jObject = new JObject();
                    jObject.Add("prdt_code", dt.Rows[i]["FNUMBER"].ToString());
                    jObject.Add("qty", Convert.ToInt32(dt.Rows[i]["FQTY"]));
                    Jsonarray.Add(jObject);
                }
                @params["details"] = Jsonarray.ToString();
                Httpclient httpclient = new Httpclient();
                string json = httpclient.ZWLFRequstPost(@params, param.url, param.AppSecret);
                JObject OrderJson = JsonConvert.DeserializeObject<JObject>(json);
                string code = OrderJson["code"].ToString();
                string upsql = "";
                if (code == "200")
                {
                    string bil_no = OrderJson["data"]["bil_no"].ToString();
                    upsql = string.Format(@"/*dialect*/ update T_STK_STKTRANSFERIN set F_ZWLF_PUSHSTATE=1,F_ZWLF_PUSHTIME=GETDATE(),
                                                                  F_ZWLF_OUTNO='{0}' where  FBILLNO='{1}'", bil_no, dt.Rows[0]["FBILLNO"].ToString());
                    retult = true;

                }
                else
                {
                    string msg = "生成胜途的其他出库单失败：" + OrderJson["msg"].ToString();
                    throw new Exception(msg);
                }
                DBServiceHelper.Execute(Context, upsql);

            }
            catch (KDException ex)
            {
                throw new Exception(ex.ToString());
            }
            return retult;
        }
    }
}
