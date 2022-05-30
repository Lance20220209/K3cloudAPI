using AnyCubicK3cloudProject;
using Kingdee.BOS;
using Kingdee.BOS.Core.DynamicForm;
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
    [Description("组装拆卸单审核生成生成胜途对应其他出入库单据")]
    [Kingdee.BOS.Util.HotUpdate]
    public class ZWLF_STK_AssembledAppAudit : AbstractOperationServicePlugIn
    {
        /// <summary>
        /// 定制加载指定字段到实体里<p>
        /// </summary>
        /// <param name="e">事件对象</param>
        public override void OnPreparePropertys(Kingdee.BOS.Core.DynamicForm.PlugIn.Args.PreparePropertysEventArgs e)
        {
            base.OnPreparePropertys(e);
            e.FieldKeys.Add("FAffairType");
        }
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
                        //判断是组装还是拆卸
                        string AffairType = item["AffairType"].ToString();
                        //配置表
                        string sql = string.Format(@"/*dialect*/ select  top 1 app_key,AppSecret,date_type,end_date,page_no,page_size,
                                     username,password,url,wh_code ,method,start_date,access_token
                                    from ZWLF_T_Configuration where  IsDisable=0");
                        //成品
                        sql += string.Format(@"/*dialect*/ select d.FBILLNO, d.F_ZWLF_PUSHSTATE,t.FNUMBER as wh_code,a.FSTOCKID,b.FNUMBER as wlFNUMBER, FQTY ,d.FDATE 
                                                        from  T_STK_ASSEMBLYPRODUCT  a
                                                        inner join T_BD_MATERIAL b on a.FMATERIALID=b.FMATERIALID
                                                        inner join T_BD_STOCK  t on t.FSTOCKID=a.FSTOCKID
                                                        inner  join T_STK_ASSEMBLY d on d.FID=a.FID
                                                        where a.FSTOCKID in (171002,103583,226441,171005,270325,102502,450697,450698) and d.FID={0}", Id);
                        //子项表
                        sql += string.Format(@"/*dialect*/ select d.FBILLNO,t.FNUMBER as wh_code ,d.F_ZWLF_PUSHSTATE,b.FNUMBER as wlFNUMBER,a.FSTOCKID , a.FQTY ,d.FDATE from  T_STK_ASSEMBLYSUBITEM a 
                                                        inner join T_BD_MATERIAL b on a.FMATERIALID=b.FMATERIALID
                                                        inner join T_BD_STOCK  t on t.FSTOCKID=a.FSTOCKID
                                                        inner join T_STK_ASSEMBLYPRODUCT c on c.FENTRYID=a.FENTRYID
                                                        inner join T_STK_ASSEMBLY d on d.FID=c.FID
                                                        where a.FSTOCKID in (171002,103583,226441,171005,270325,102502,450697,450698) and d.FID={0}", Id);
                        DataSet ds = DBServiceHelper.ExecuteDataSet(Context, sql);
                        DataTable dt = ds.Tables[0];
                        DataTable dt_c = ds.Tables[1];
                        DataTable dt_z = ds.Tables[2];
                        if (dt_c.Rows.Count > 0 || dt_z.Rows.Count > 0)
                        {
                            //只有未推送成功的状态才会推送
                            if (dt_c.Rows[0]["F_ZWLF_PUSHSTATE"].ToString() != "1")
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
                                        bool retult = STOtheroutIn(parameters, ds, Id, AffairType);
                                    }
                                }

                            }
                        }
                    }
                }
            }
        }
        /// <summary>
        /// 生成胜途其他出入库单据
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public bool STOtheroutIn(Parameters param, DataSet ds, long FID, string AffairType)
        {
            bool retult = true;
            try
            {
                DataTable dt = ds.Tables[0];
                DataTable dt_c = ds.Tables[1];
                DataTable dt_z = ds.Tables[2];
                string F_ZWLF_PUSHSTATE = "0";
                string upsql = "";
                string method_1 = "";
                string method_2 = "";
                bool iSsusess = true;
                if (AffairType == "Assembly")
                {
                    method_1 = "frdas.erp.otherin.add";
                    method_2 = "frdas.erp.otherout.add";
                }
                else
                {
                    method_1 = "frdas.erp.otherout.add";
                    method_2 = "frdas.erp.otherin.add";

                }
               
                if (dt_z.Rows.Count > 0)
                {
                    string wh_code = "";
                    //仓库编码
                    if (dt_z.Rows[0]["FSTOCKID"].ToString() == "103583")
                    {
                        wh_code = "CK002";
                    }
                    else if (dt_z.Rows[0]["FSTOCKID"].ToString() == "102502")
                    {
                        wh_code = "020";
                    }
                    else if (dt_z.Rows[0]["FSTOCKID"].ToString() == "171002")
                    {
                        wh_code = "014";
                    }
                    else if (dt_z.Rows[0]["FSTOCKID"].ToString() == "171005")
                    {
                        wh_code = "017";
                    }
                    else if (dt_z.Rows[0]["FSTOCKID"].ToString() == "226441")
                    {
                        wh_code = "019";
                    }
                    else if (dt_z.Rows[0]["FSTOCKID"].ToString() == "270325")
                    {
                        wh_code = "018";
                    }
                    else if (dt.Rows[0]["FSTOCKID"].ToString() == "450697")
                    {
                        wh_code = "CK029";
                    }
                    else if (dt.Rows[0]["FSTOCKID"].ToString() == "450698")
                    {
                        wh_code = "CK030";
                    }
                    //应用级输入参数：
                    Dictionary<string, string> @params = new Dictionary<string, string>();
                    @params.Add("method", method_2);
                    @params.Add("app_key", param.app_key);
                    @params.Add("sign_method", "md5");
                    // @params.Add("sign", zWLF.sign); 
                    @params.Add("session", param.access_token);
                    @params.Add("timestamp", DateTime.Now.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                    @params.Add("format", "json");
                    @params.Add("v", "1.0");
                    //业务输入参数：
                    //单据日期
                    @params["bil_date"] = dt_z.Rows[0]["FDATE"].ToString();
                    //单据类型
                    @params["bil_type"] = "组装拆卸单";
                    //外部单据编码
                    @params["refer_no"] = dt_z.Rows[0]["FBILLNO"].ToString();
                    //仓库编码
                    @params["wh_code"] = wh_code;
                    //公司名称
                    @params["branch_name"] = "Anycubic";
                    //备注 
                    @params["memo_info"] = "金蝶的组转拆卸单生成胜途的其他出库单";
                    //参与库龄分析 
                    @params["is_stock_age"] = "True";
                    //立即审核 
                    @params["audit"] = "True";
                    //商品信息明细
                    JArray Jsonarray = new JArray();
                    for (int i = 0; i < dt_z.Rows.Count; i++)
                    {
                        JObject jObject = new JObject();
                        jObject.Add("prdt_code", dt_z.Rows[i]["wlFNUMBER"].ToString());
                        jObject.Add("qty", Convert.ToInt32(dt_z.Rows[i]["FQTY"]));
                        Jsonarray.Add(jObject);
                    }
                    @params["details"] = Jsonarray.ToString();
                    Httpclient httpclient = new Httpclient();
                    string json = httpclient.ZWLFRequstPost(@params, param.url, param.AppSecret);
                    JObject OrderJson = JsonConvert.DeserializeObject<JObject>(json);
                    string code = OrderJson["code"].ToString();
                    if (code == "200")
                    {
                        if (AffairType == "Assembly")
                        {
                            string bil_no = OrderJson["data"]["bil_no"].ToString();
                            upsql += string.Format(@"/*dialect*/ update T_STK_ASSEMBLY set F_ZWLF_PUSHSTATE='{2}',F_ZWLF_PUSHTIME=GETDATE(),
                                                              F_ZWLF_OutNo='{0}',F_ZWLF_NOTE='生成胜途其他出库单成功' where  FBILLNO='{1}'", bil_no, dt_z.Rows[0]["FBILLNO"].ToString(), F_ZWLF_PUSHSTATE);

                        }
                        else
                        {

                            string bil_no = OrderJson["data"]["bil_no"].ToString();
                            upsql += string.Format(@"/*dialect*/ update T_STK_ASSEMBLY set F_ZWLF_PUSHTIME=GETDATE(),
                                           F_ZWLF_InNo='{0}' where  FBILLNO='{1}'", bil_no, dt_c.Rows[0]["FBILLNO"].ToString());
                            F_ZWLF_PUSHSTATE = "1";
                        }
                    }
                    else
                    {
                        iSsusess = false;
                        if (json.Contains("外部单号已存在!"))
                        {
                            F_ZWLF_PUSHSTATE = "1";
                            upsql += string.Format(@"/*dialect*/ update T_STK_ASSEMBLY set F_ZWLF_PUSHSTATE='{2}',F_ZWLF_PUSHTIME=GETDATE(),
                         F_ZWLF_NOTE='{0}' where  FBILLNO='{1}'", "生成胜途其他出库单成功", dt_z.Rows[0]["FBILLNO"].ToString(), F_ZWLF_PUSHSTATE);
                        }
                        else
                        {
                            F_ZWLF_PUSHSTATE = "0";
                            string msg = "生成胜途其他出库单失败：" + OrderJson["msg"].ToString();
                            upsql += string.Format(@"/*dialect*/ update T_STK_ASSEMBLY set F_ZWLF_PUSHSTATE='{2}',F_ZWLF_PUSHTIME=GETDATE(),
                         F_ZWLF_NOTE=F_ZWLF_NOTE+'{0}' where  FBILLNO='{1}'", msg, dt_z.Rows[0]["FBILLNO"].ToString(), F_ZWLF_PUSHSTATE);
                            //反审核单据
                            List<KeyValuePair<object, object>> lstKeyValuePairs = new List<KeyValuePair<object, object>>();
                            KeyValuePair<object, object> keyValuePair = new KeyValuePair<object, object>(FID, "");
                            lstKeyValuePairs.Add(keyValuePair);
                            FormMetadata meta = MetaDataServiceHelper.Load(Context, "STK_AssembledApp") as FormMetadata;
                            BusinessInfo info = meta.BusinessInfo;
                            //反审核id为10001的当前单据
                            IOperationResult unAuditResult = BusinessDataServiceHelper.SetBillStatus(this.Context, info, lstKeyValuePairs, null, "UnAudit");

                        }
                    }
                    
                }
              
                if (dt_c.Rows.Count > 0)
                {
                    
                    if (iSsusess)
                    {
                        string wh_code = "";
                        //仓库编码
                        if (dt_z.Rows[0]["FSTOCKID"].ToString() == "103583")
                        {
                            wh_code = "CK002";
                        }
                        else if (dt_z.Rows[0]["FSTOCKID"].ToString() == "102502")
                        {
                            wh_code = "020";
                        }
                        else if (dt_z.Rows[0]["FSTOCKID"].ToString() == "171002")
                        {
                            wh_code = "014";
                        }
                        else if (dt_z.Rows[0]["FSTOCKID"].ToString() == "171005")
                        {
                            wh_code = "017";
                        }
                        else if (dt_z.Rows[0]["FSTOCKID"].ToString() == "226441")
                        {
                            wh_code = "019";
                        }
                        else if (dt_z.Rows[0]["FSTOCKID"].ToString() == "270325")
                        {
                            wh_code = "018";
                        }
                        else if (dt.Rows[0]["FSTOCKID"].ToString() == "450697")
                        {
                            wh_code = "CK029";
                        }
                        else if (dt.Rows[0]["FSTOCKID"].ToString() == "450698")
                        {
                            wh_code = "CK030";
                        }

                        //应用级输入参数：
                        Dictionary<string, string> @params = new Dictionary<string, string>();
                       @params.Add("method", method_1);
                       @params.Add("app_key", param.app_key);
                       @params.Add("sign_method", "md5");
                       // @params.Add("sign", zWLF.sign); 
                       @params.Add("session", param.access_token);
                       @params.Add("timestamp", DateTime.Now.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                       @params.Add("format", "json");
                       @params.Add("v", "1.0");
                       //业务输入参数：
                       //单据日期
                       @params["bil_date"] = dt_c.Rows[0]["FDATE"].ToString();
                       //单据类型
                       @params["bil_type"] = "组装拆卸单";
                       //外部单据编码
                       @params["refer_no"] = dt_c.Rows[0]["FBILLNO"].ToString();
                        @params["wh_code"] = wh_code;
                       //公司名称
                       @params["branch_name"] = "Anycubic";
                       //备注 
                       @params["memo_info"] = "金蝶的组转拆卸单生成胜途的其他入库单";
                       //参与库龄分析 
                       @params["is_stock_age"] = "True";
                       //立即审核 
                       @params["audit"] = "True";
                       //商品信息明细
                       JArray Jsonarray = new JArray();
                       for (int i = 0; i < dt_c.Rows.Count; i++)
                       {
                           JObject jObject = new JObject();
                           jObject.Add("prdt_code", dt_c.Rows[i]["wlFNUMBER"].ToString());
                           jObject.Add("qty", Convert.ToInt32(dt_c.Rows[i]["FQTY"]));
                           Jsonarray.Add(jObject);
                       }
                       @params["details"] = Jsonarray.ToString();
                       Httpclient httpclient = new Httpclient();
                       string json = httpclient.ZWLFRequstPost(@params, param.url, param.AppSecret);
                       JObject OrderJson = JsonConvert.DeserializeObject<JObject>(json);
                       string code = OrderJson["code"].ToString();
                       if (code == "200")
                       {
                           if (AffairType == "Assembly")
                           {
                               string bil_no = OrderJson["data"]["bil_no"].ToString();
                               upsql += string.Format(@"/*dialect*/ update T_STK_ASSEMBLY set F_ZWLF_PUSHTIME=GETDATE(),
                                          F_ZWLF_InNo='{0}' where  FBILLNO='{1}'", bil_no, dt_c.Rows[0]["FBILLNO"].ToString());
                               F_ZWLF_PUSHSTATE = "1";
                           }
                           else
                           {
                               string bil_no = OrderJson["data"]["bil_no"].ToString();
                               upsql += string.Format(@"/*dialect*/ update T_STK_ASSEMBLY set F_ZWLF_PUSHSTATE='{2}',F_ZWLF_PUSHTIME=GETDATE(),
                                                             F_ZWLF_OutNo='{0}',F_ZWLF_NOTE='生成胜途其他出库单成功' where  FBILLNO='{1}'", bil_no, dt_z.Rows[0]["FBILLNO"].ToString(), F_ZWLF_PUSHSTATE);

                           }
                       }
                       else
                       {
                           if (json.Contains("外部单号已存在!"))
                           {
                               F_ZWLF_PUSHSTATE = "1";
                               upsql += string.Format(@"/*dialect*/ update T_STK_ASSEMBLY set F_ZWLF_PUSHTIME=GETDATE(),
                                         F_ZWLF_NOTE='{0}' where  FBILLNO='{1}'", "成功", dt_c.Rows[0]["FBILLNO"].ToString());
                           }
                           else
                           {
                               string msg = "生成胜途其他入库单失败：" + OrderJson["msg"].ToString();
                               upsql += string.Format(@"/*dialect*/ update T_STK_ASSEMBLY set F_ZWLF_PUSHTIME=GETDATE(),
                                         F_ZWLF_NOTE='{0}' where  FBILLNO='{1}'", msg, dt_c.Rows[0]["FBILLNO"].ToString());

                           }
                       }
                    }
                }
                if (upsql != "")
                {
                    DBServiceHelper.Execute(Context, upsql);
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
