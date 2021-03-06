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
    [Description("采购退料单审核推送胜途")]
    [Kingdee.BOS.Util.HotUpdate]
    public class ZWLF_PUR_MRBAudit : AbstractOperationServicePlugIn
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
                        //对应胜途入库退回单
                        sql += string.Format(@"/*dialect*/select  FBILLNO,FDATE, FSTOCKORGID,b.FSTOCKID as FSTOCKID,a.FAPPROVEDATE ,wl.FNUMBER, FRMREALQTY as FQTY, b.FNOTE  from  t_PUR_MRB a 
                                                          join T_PUR_MRBENTRY b on a.FID=b.FID
                                                          join T_BD_MATERIAL wl on wl.FMATERIALID=b.FMATERIALID
                                                          left join T_PUR_MRBFIN c on c.fid=b.FID
                                                          where a.FMRMODE!='A' and  a.F_ZWLF_PUSHSTATE!='1' and FISGENFORIOS=0
                                                          and  a.FID={0} and b.FSTOCKID in (171002,103583,226441,171005,270325,102502,450697,450698)", FID);
                        DataSet ds = DBServiceHelper.ExecuteDataSet(Context, sql);
                        DataTable dt = ds.Tables[0];
                        DataTable dt1 = ds.Tables[1];
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
                                    //其他出库单
                                    if (dt1.Rows.Count > 0)
                                    {
                                        STPurback(parameters, dt1);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        /// <summary>
        /// 生成胜途入库退回单
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public bool STPurback(Parameters param, DataTable dt)
        {
            bool retult = true;
            try
            {
                //应用级输入参数：
                Dictionary<string, string> @params = new Dictionary<string, string>();
                @params.Add("method", "frdas.erp.purback.add");
                @params.Add("app_key", param.app_key);
                @params.Add("sign_method", "md5");
                // @params.Add("sign", zWLF.sign); 
                @params.Add("session", param.access_token);
                @params.Add("timestamp", DateTime.Now.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                @params.Add("format", "json");
                @params.Add("v", "1.0");
                //外部单据编码
                @params["refer_no"] = dt.Rows[0]["FBILLNO"].ToString();
                //单据日期
                @params["bil_date"] = Convert.ToDateTime(dt.Rows[0]["FDATE"]).ToString("yyyy-MM-dd");
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
                //供应商编码
                @params["cust_code"] = "101";
                //公司名称
                @params["branch_name"] = "Anycubic";//纵维接口测试;
                 //货币代码
                @params["cur_code"] = "CNY";
                //部门编码 
                @params["dept_code"] = "D10101";
                //部门编码 
               // @params["salm_code"] = "";
                //跟踪号
                //@params["logistics_no"] = "";
                //退回运费
                @params["post_fee"] = "0";
                //备注 
                @params["memo_info"] = "金蝶的采购退料单" + dt.Rows[0]["FBILLNO"].ToString() + "单生成胜途的入库退回单";
                //制单人
                @params.Add("crt_user", "超级管理员");
                //立即审核 
                @params["audit"] = "True";
                //退货原由
                //@params["back_reason"] = "退货原由" + dt.Rows[0]["FNOTE"].ToString();
                //审核日期
                @params.Add("chk_time", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                //审核日期
                @params.Add("chk_user", "超级管理员");
                //商品信息明细
                JArray Jsonarray = new JArray();
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    JObject jObject = new JObject();
                    jObject.Add("prdt_code", dt.Rows[i]["FNUMBER"].ToString());
                    jObject.Add("qty", Convert.ToInt32(dt.Rows[i]["FQTY"]));
                    jObject.Add("tax_rto", "0");
                    jObject.Add("amt_tax", "0");
                    jObject.Add("is_free", "False");
                    jObject.Add("memo_info", "金蝶的采购退料单" + dt.Rows[0]["FBILLNO"].ToString() + "单生成胜途的入库退回单"); 
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
                    upsql = string.Format(@"/*dialect*/ update t_PUR_MRB set F_ZWLF_PUSHSTATE=1,F_ZWLF_PUSHTIME=GETDATE(),
                                                                  F_ZWLF_RENO='{0}' where  FBILLNO='{1}'", bil_no, dt.Rows[0]["FBILLNO"].ToString());
                    retult = true;

                }
                else
                {
                    string msg = "生成胜途的入库退回单失败：" + OrderJson["msg"].ToString();
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
