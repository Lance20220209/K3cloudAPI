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
    [Description("其他出库单审核生成胜途的其他出库单")]
    [Kingdee.BOS.Util.HotUpdate]
    public class ZWLF_STK_MisDeliveryAudit : AbstractOperationServicePlugIn
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
                        sql += string.Format(@"/*dialect*/ select d.FBILLNO,a.FSTOCKID, d.F_ZWLF_PUSHSTATE,b.FNUMBER as wlFNUMBER,FQTY,d.FDATE 
                                                           from  T_STK_MISDELIVERYENTRY   a
                                                           inner join T_BD_MATERIAL b on a.FMATERIALID=b.FMATERIALID
                                                           inner  join T_STK_MISDELIVERY d on d.FID=a.FID
                                                           where   d.F_ZWLF_PUSHSTATE !='1' and FSTOCKDIRECT='GENERAL' and a.FSTOCKID in (171002,103583,226441,171005,270325,102502,450697,450698) and d.FID={0}", Id);
                        //其他出库单退货
                        sql += string.Format(@"/*dialect*/ select d.FBILLNO,a.FSTOCKID, d.F_ZWLF_PUSHSTATE,b.FNUMBER as wlFNUMBER,FQTY,d.FDATE 
                                                           from  T_STK_MISDELIVERYENTRY   a
                                                           inner join T_BD_MATERIAL b on a.FMATERIALID=b.FMATERIALID
                                                           inner  join T_STK_MISDELIVERY d on d.FID=a.FID
                                                           where   d.F_ZWLF_PUSHSTATE !='1' and FSTOCKDIRECT='RETURN' and a.FSTOCKID in (171002,103583,226441,171005,270325,102502,450697,450698) and d.FID={0}", Id);
                        DataSet ds = DBServiceHelper.ExecuteDataSet(Context, sql);
                        DataTable dt = ds.Tables[0];
                        DataTable dt_c = ds.Tables[1];
                        DataTable dt_t = ds.Tables[2];
                        //获取授权
                        if (dt.Rows.Count > 0)
                        {
                            Parameters parameters = new Parameters();
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
                            if (dt_c.Rows.Count > 0)
                            {
                                bool retult = STOtheroutIn(parameters, dt_c,"1");
                            }
                            //其他出库退货生成胜途其他入库
                            if (dt_t.Rows.Count > 0)
                            {
                                ZWLF_STK_MISCELLANEOUS zWLF_STK_MISCELLANEOUS = new ZWLF_STK_MISCELLANEOUS();
                                bool retult = zWLF_STK_MISCELLANEOUS.STOtheroutIn(parameters, dt_t, "2");
                            }

                        }
                    }
                }
            }
        }

        /// <summary>
        /// 生成胜途其他出库单据
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public bool STOtheroutIn(Parameters param, DataTable dt_c, string type)
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
                @params["bil_date"] = dt_c.Rows[0]["FDATE"].ToString();
                //单据类型
                @params["bil_type"] = "其他出库";
                //外部单据编码
                @params["refer_no"] = dt_c.Rows[0]["FBILLNO"].ToString();
                //仓库编码
                //@params["wh_code"] = "CK002";
                //仓库编码
                if (dt_c.Rows[0]["FSTOCKID"].ToString() == "103583")
                {
                    @params["wh_code"] = "CK002";
                }
                else if (dt_c.Rows[0]["FSTOCKID"].ToString() == "102502")
                {
                    @params["wh_code"] = "020";
                }
                else if (dt_c.Rows[0]["FSTOCKID"].ToString() == "171002")
                {
                    @params["wh_code"] = "014";
                }
                else if (dt_c.Rows[0]["FSTOCKID"].ToString() == "171005")
                {
                    @params["wh_code"] = "017";
                }
                else if (dt_c.Rows[0]["FSTOCKID"].ToString() == "226441")
                {
                    @params["wh_code"] = "019";
                }
                else if (dt_c.Rows[0]["FSTOCKID"].ToString() == "270325")
                {
                    @params["wh_code"] = "018";
                }
                else if (dt_c.Rows[0]["FSTOCKID"].ToString() == "450697")
                {
                    @params["wh_code"] = "CK029";
                }
                else if (dt_c.Rows[0]["FSTOCKID"].ToString() == "450698")
                {
                    @params["wh_code"] = "CK030";
                }
                //公司名称
                @params["branch_name"] = "Anycubic";//纵维接口测试;
                if (type == "1")
                {
                    //备注 
                    @params["memo_info"] = "金蝶的其他出库单生成胜途的其他出库单";
                }
                else
                {
                    //备注 
                    @params["memo_info"] = "金蝶的其他入库退货生成胜途的其他出库单";
                }
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
                string upsql = "";
                if (code == "200")
                {
                    string bil_no = OrderJson["data"]["bil_no"].ToString();
                    upsql = string.Format(@"/*dialect*/ update T_STK_MISDELIVERY set F_ZWLF_PUSHSTATE=1,F_ZWLF_PUSHTIME=GETDATE(),
                                                                  F_ZWLF_OUTNO='{0}' where  FBILLNO='{1}'", bil_no, dt_c.Rows[0]["FBILLNO"].ToString());

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
