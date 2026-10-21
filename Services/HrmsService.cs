using System.Text;
using System.Text.Json;
using System.Web;

namespace SystemAll.Services;

public class HrmsService
{
    private readonly IHttpClientFactory _httpFactory;
    private const string HrmsUrl = "http://cptsvs547:1000/middleware/oracle/hrms";

    public HrmsService(IHttpClientFactory httpFactory)
    {
        _httpFactory = httpFactory;
    }

    public async Task<HrmsEmployee?> GetEmployeeAsync(string empNo)
    {
        if (!IsValidEmpNo(empNo)) return null;

        var safeEmp = empNo.Replace("'", "").Trim();

        var sql =
            "select EMP_NO, GNAME_ENG, FNAME_ENG, POSN_ENAME, POSN_CODE, BAND, " +
            "DIV_NAME, DIV_ABB_NAME, DEPT_CODE, DEPT_ABB_NAME, EMAIL " +
            "from ADMIN.v_emp_data_all_cpt where UPPER(emp_no)=UPPER('" + safeEmp + "')";

        var body = "command=" + HttpUtility.UrlEncode(sql);

        var client = _httpFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(15);

        var content = new StringContent(body, Encoding.UTF8, "application/x-www-form-urlencoded");
        var res = await client.PostAsync(HrmsUrl, content);
        res.EnsureSuccessStatusCode();

        var json = await res.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("data", out var dataEl)
            || dataEl.ValueKind != JsonValueKind.Array
            || dataEl.GetArrayLength() == 0)
            return null;

        var u = dataEl[0];

        return new HrmsEmployee
        {
            EmpNo = GetStr(u, "EMP_NO"),
            GnameEng = GetStr(u, "GNAME_ENG"),
            FnameEng = GetStr(u, "FNAME_ENG"),
            PosnEname = GetStr(u, "POSN_ENAME"),
            PosnCode = GetStr(u, "POSN_CODE"),
            Band = GetStr(u, "BAND"),
            DivName = GetStr(u, "DIV_NAME"),
            DivAbb = GetStr(u, "DIV_ABB_NAME"),
            DeptCode = GetStr(u, "DEPT_CODE"),
            DeptAbb = GetStr(u, "DEPT_ABB_NAME"),
            Email = GetStr(u, "EMAIL")
        };
    }

    // อ่าน property แบบปลอดภัย — กัน CS8602 (null reference) ที่ line 41,44 ด้วย
    private static string GetStr(JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() ?? ""
            : "";

    private static bool IsValidEmpNo(string empNo)
        => !string.IsNullOrWhiteSpace(empNo)
           && empNo.Length <= 20
           && empNo.All(char.IsLetterOrDigit);
}

public class HrmsEmployee
{
    public string EmpNo { get; set; } = "";
    public string GnameEng { get; set; } = "";
    public string FnameEng { get; set; } = "";
    public string PosnEname { get; set; } = "";
    public string PosnCode { get; set; } = "";
    public string Band { get; set; } = "";
    public string DivName { get; set; } = "";
    public string DivAbb { get; set; } = "";
    public string DeptCode { get; set; } = "";
    public string DeptAbb { get; set; } = "";
    public string Email { get; set; } = "";

    public string FullName => (GnameEng + " " + FnameEng).Trim();
    public bool IsEng => DivAbb?.Trim().ToUpper() == "ENG";
    public bool IsEng4 => DeptCode?.Trim() == "5390" || DeptAbb?.Trim().ToUpper() == "ENG4";
}