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
using System.Text;
using System.Threading.Tasks;

namespace Anycubic.DC.API
{
    [Description("物料审核推送胜途")]
    [Kingdee.BOS.Util.HotUpdate]
    public class ZWLF_Material_Add : AbstractOperationServicePlugIn
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
                        long FMATERIALID = Convert.ToInt64(item["Id"].ToString());
                        //配置表
                        string sql = string.Format(@"/*dialect*/ select  top 1 app_key,AppSecret,date_type,end_date,page_no,page_size,
                                     username,password,url,wh_code ,method,start_date,access_token
                                    from ZWLF_T_Configuration where  IsDisable=0");
                        //物料表
                        sql += string.Format(@"/*dialect*/ select  a.FNUMBER as WlFnumber , b.FNAME as wlFname ,isnull(FSPECIFICATION,'') as FSPECIFICATION,d.FNAME as dwFname
                                                       ,case when YDNumber is  null then 'D002' else YDNumber end  as FZBM ,isnull(FBARCODE,'') as FBARCODE,a.FFORBIDSTATUS,
                                                       case when d1.FName='克' and FNETWEIGHT<>'0.0' then cast(FNETWEIGHT as decimal(14,4)) 
                                                       when d1.FName='千克' and FNETWEIGHT<>'0.0' then cast(FNETWEIGHT as decimal(14,4))*1000 else 0 end as FNETWEIGHT,
                                                       case when d2 .FName='米' and  FLENGTH<>'0.0' then cast(FLENGTH as decimal(14,4))*100 else 0 end as FLENGTH,
                                                       case when d2 .FName='米' and  FWIDTH<>'0.0' then cast(FWIDTH as decimal(14,4))*100 else 0 end as FWIDTH,
                                                       case when d2 .FName='米' and  FHEIGHT<>'0.0' then cast(FHEIGHT as decimal(14,4))*100 else 0 end as FHEIGHT
                                                       from  T_BD_MATERIAL a
                                                       join T_BD_MATERIAL_L b  on a.FMATERIALID=b.FMATERIALID
                                                       join t_BD_MaterialBase c on c.FMATERIALID=a.FMATERIALID
                                                       left join  T_BD_UNIT_L d on d.FUNITID=c.FBASEUNITID
                                                       left join  T_BD_UNIT_L d1 on d1.FUNITID=c.FWEIGHTUNITID
                                                       left join  T_BD_UNIT_L d2 on d2.FUNITID=c.FVOLUMEUNITID
                                                       left join T_BD_MATERIALGROUP e on  a.FMATERIALGROUP=e.FID
                                                       left join ZWLF_T_YQBaseRelation f on f.YSNUmber=e.FNUMBER
                                                       where FErpClsID<>'10' and a.FUSEORGID=1 and a.FMATERIALID={0}
                                                       ", FMATERIALID);
                        DataSet ds = DBServiceHelper.ExecuteDataSet(Context, sql);
                        DataTable dt = ds.Tables[0];
                        DataTable dt_wl = ds.Tables[1];
                        if (dt_wl.Rows.Count > 0)
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
                                    SearchMaterial(parameters, dt_wl);
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 查询商品
        /// </summary>
        /// <param name="param"></param>
        /// <param name="ds"></param>
        /// <returns></returns>
        public void  SearchMaterial(Parameters param, DataTable dt)
        {
            //应用级输入参数：
            Dictionary<string, string> @params = new Dictionary<string, string>();
            @params.Add("method", "frdas.base.items.get");
            @params.Add("app_key", param.app_key);
            @params.Add("sign_method", "md5");
            // @params.Add("sign", zWLF.sign); 
            @params.Add("session", param.access_token);
            @params.Add("timestamp", DateTime.Now.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
            @params.Add("format", "json");
            @params.Add("v", "1.0");
            @params.Add("prdt_code", dt.Rows[0]["WlFnumber"].ToString());
            //查询该商品是否已经存在
            Httpclient httpclient = new Httpclient();
            string json = httpclient.ZWLFRequstPost(@params, param.url, param.AppSecret);
            JObject OrderJson = JsonConvert.DeserializeObject<JObject>(json);
            string code = OrderJson["code"].ToString();
            string upsql = "";
            if (code == "200")
            {
                int total_count = Convert.ToInt32(OrderJson["data"]["total_count"].ToString()); //本次返回的订单数
                //胜途存在该商品
                if(total_count > 0)
                {
                    bool result=Editaterial(param, dt);
                    if (!result)
                    {
                        throw new Exception("推送物料到胜途失败！");
                    }
                }
                else
                {
                    bool result = AddMaterial(param,  dt);
                    if (!result)
                    {
                        throw new Exception("推送物料到胜途失败！");
                    }
                }
            }
            else
            {
                string msg = "推送胜途失败：" + OrderJson["msg"].ToString();
                throw new Exception(msg);
            }
        }

        /// <summary>
        /// 修改商品
        /// </summary>
        /// <param name="param"></param>
        /// <param name="ds"></param>
        /// <returns></returns>
        public bool Editaterial(Parameters param, DataTable dt)
        {
            bool result = true;
            try
            {
                //应用级输入参数：
                Dictionary<string, string> @params = new Dictionary<string, string>();
                @params.Add("method", "frdas.base.items.edit");
                @params.Add("app_key", param.app_key);
                @params.Add("sign_method", "md5");
                // @params.Add("sign", zWLF.sign); 
                @params.Add("session", param.access_token);
                @params.Add("timestamp", DateTime.Now.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                @params.Add("format", "json");
                @params.Add("v", "1.0");
                @params.Add("prdt_code", dt.Rows[0]["WlFnumber"].ToString());
                @params.Add("prdt_name", dt.Rows[0]["wlFname"].ToString());
                if (!string.IsNullOrEmpty(dt.Rows[0]["FSPECIFICATION"].ToString().Trim()))
                {
                    @params.Add("spc", dt.Rows[0]["FSPECIFICATION"].ToString());
                }
                @params.Add("unit_name", dt.Rows[0]["dwFname"].ToString());
                @params.Add("branch_name", "Anycubic");
                @params.Add("company", "Anycubic");
                if (!string.IsNullOrEmpty(dt.Rows[0]["FBARCODE"].ToString().Trim()))
                {
                    @params.Add("bar_code", Math.Round(Convert.ToDouble(dt.Rows[0]["FBARCODE"].ToString().Trim()), 2).ToString());
                }
                if (!string.IsNullOrEmpty(dt.Rows[0]["FZBM"].ToString().Trim()))
                {
                    @params.Add("prdt_type", dt.Rows[0]["FZBM"].ToString().Trim());
                }
                if (dt.Rows[0]["FLENGTH"].ToString() != "0.00")
                {
                    @params.Add("length", Math.Round(Convert.ToDouble(dt.Rows[0]["FLENGTH"].ToString()), 2).ToString());
                }
                if (dt.Rows[0]["FWIDTH"].ToString() != "0.00")
                {
                    @params.Add("width", Math.Round(Convert.ToDouble(dt.Rows[0]["FWIDTH"].ToString()), 2).ToString());
                }
                if (dt.Rows[0]["FHEIGHT"].ToString() != "0.00")
                {
                    @params.Add("height", Math.Round(Convert.ToDouble(dt.Rows[0]["FHEIGHT"].ToString()), 2).ToString());
                }
                if (dt.Rows[0]["FNETWEIGHT"].ToString() != "0.00")
                {
                    @params.Add("weight", Math.Round(Convert.ToDouble(Convert.ToDouble(dt.Rows[0]["FNETWEIGHT"]).ToString()), 2).ToString());
                }
                @params.Add("prdt_category", "1");
                @params.Add("fac_meas", Convert.ToDouble(dt.Rows[0]["FLENGTH"]).ToString() + "*" + Convert.ToDouble(dt.Rows[0]["FWIDTH"]).ToString() + "*" + Convert.ToDouble(dt.Rows[0]["FHEIGHT"]).ToString());
                Httpclient httpclient = new Httpclient();
                string json = httpclient.ZWLFRequstPost(@params, param.url, param.AppSecret);
                JObject OrderJson = JsonConvert.DeserializeObject<JObject>(json);
                string code = OrderJson["code"].ToString();
                string upsql = "";
                if (code == "200")
                {
                    string msg = "修改成功："+OrderJson["msg"].ToString();
                    upsql = string.Format(@"/*dialect*/ update T_BD_MATERIAL set F_ZWLF_PUSHSTATE=1,F_ZWLF_PUSHTIME=GETDATE(),
                                                                  F_ZWLF_NOTE='{0}' where  FNUMBER='{1}'", msg, dt.Rows[0]["WlFnumber"].ToString());
                    DBServiceHelper.Execute(Context, upsql);
                }
                else
                {
                    string msg = "修改胜途商品失败：" + OrderJson["msg"].ToString();
                    throw new Exception(msg);
                }
            }
            catch (KDException ex)
            {
                result = false;
                throw new Exception(ex.ToString());
            }
            return result;
        }
        /// <summary>
        /// 新增商品
        /// </summary>
        /// <param name="param"></param>
        /// <param name="ds"></param>
        /// <returns></returns>
        public bool AddMaterial(Parameters param, DataTable dt)
        {
            bool result = true;
            try
            {
                //应用级输入参数：
                Dictionary<string, string> @params = new Dictionary<string, string>();
                @params.Add("method", "frdas.base.items.add");
                @params.Add("app_key", param.app_key);
                @params.Add("sign_method", "md5");
                // @params.Add("sign", zWLF.sign); 
                @params.Add("session", param.access_token);
                @params.Add("timestamp", DateTime.Now.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                @params.Add("format", "json");
                @params.Add("v", "1.0");
                @params.Add("prdt_code", dt.Rows[0]["WlFnumber"].ToString());
                @params.Add("prdt_name", dt.Rows[0]["wlFname"].ToString());
                if (!string.IsNullOrEmpty(dt.Rows[0]["FSPECIFICATION"].ToString().Trim()))
                {
                    @params.Add("spc", dt.Rows[0]["FSPECIFICATION"].ToString());
                }
                @params.Add("unit_name", dt.Rows[0]["dwFname"].ToString());
                @params.Add("branch_name", "Anycubic");
                @params.Add("company", "Anycubic");
                if (!string.IsNullOrEmpty(dt.Rows[0]["FBARCODE"].ToString().Trim()))
                {
                    @params.Add("bar_code", dt.Rows[0]["FBARCODE"].ToString().Trim());
                }
                if (!string.IsNullOrEmpty(dt.Rows[0]["FZBM"].ToString().Trim()))
                {
                    @params.Add("prdt_type", dt.Rows[0]["FZBM"].ToString().Trim());
                }
                @params.Add("length", Math.Round(Convert.ToDouble(Convert.ToDouble(dt.Rows[0]["FLENGTH"]).ToString()), 2).ToString() );
                @params.Add("width", Math.Round(Convert.ToDouble(Convert.ToDouble(dt.Rows[0]["FWIDTH"]).ToString()), 2).ToString());
                @params.Add("height", Math.Round(Convert.ToDouble(Convert.ToDouble(dt.Rows[0]["FHEIGHT"]).ToString()), 2).ToString());
                @params.Add("weight", Math.Round(Convert.ToDouble(Convert.ToDouble(dt.Rows[0]["FNETWEIGHT"]).ToString()), 2).ToString());
                @params.Add("prdt_category", "1");
                @params.Add("fac_meas", Convert.ToDouble(dt.Rows[0]["FLENGTH"]).ToString()+"*"+ Convert.ToDouble(dt.Rows[0]["FWIDTH"]).ToString()+"*"+ Convert.ToDouble(dt.Rows[0]["FHEIGHT"]).ToString()); 
                Httpclient httpclient = new Httpclient();
                string json = httpclient.ZWLFRequstPost(@params, param.url, param.AppSecret);
                JObject OrderJson = JsonConvert.DeserializeObject<JObject>(json);
                string code = OrderJson["code"].ToString();
                string upsql = "";
                if (code == "200")
                {
                    string msg = "新增成功：" + OrderJson["msg"].ToString();
                    upsql = string.Format(@"/*dialect*/ update T_BD_MATERIAL set F_ZWLF_PUSHSTATE=1,F_ZWLF_PUSHTIME=GETDATE(),
                                   F_ZWLF_NOTE='{0}' where  FNUMBER='{1}'", msg, dt.Rows[0]["WlFnumber"].ToString());
                    DBServiceHelper.Execute(Context, upsql);
                }
                else
                {
                    string msg = "新增胜途商品失败：" + OrderJson["msg"].ToString();
                    throw new Exception(msg);
                }
            }
            catch (KDException ex)
            {
                result = false;
                throw new Exception(ex.ToString());
                
            }
            return result;
        }
    }
}
