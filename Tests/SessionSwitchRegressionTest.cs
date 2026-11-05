using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Xml;

public static class SessionSwitchRegressionTest
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Assembly addin;
    private static string assemblyPath;

    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            assemblyPath = args[0];
            WriteAssemblySettings("first-user", "first-password");
            addin = Assembly.LoadFrom(assemblyPath);
            Require((string)CallSettings("GetAuthUsername") == "first-user", "Assembly credentials must override stale host credentials.");

            object window = Activator.CreateInstance(addin.GetType("Veloxap.AddIn.Erwin.Window1", true));
            Invoke(window, "ResetApiSession");
            object firstProvider = Get(window, "authTokenProvider");
            object firstService = Get(window, "ruleService");
            var firstCookies = (CookieContainer)Get(window, "apiCookieContainer");
            firstCookies.Add(new Uri("https://session.invalid/"), new Cookie("old-session", "first-user"));
            var firstHandler = AttachFakeTransport(firstProvider, firstService);
            Require(AuthenticatedUser(firstService) == "first-user", "Initial requests must use the initial user.");
            Require(AuthenticatedUser(firstService) == "first-user" && firstHandler.LoginCount == 1, "Initial token must be cached before switching.");

            ((IList)Get(window, "rules")).Add("old cached rule");
            ((IList)Get(window, "validationRules")).Add(Activator.CreateInstance(addin.GetType("Veloxap.AddIn.Erwin.Models.Rule", true)));
            Set(window, "loadedRulesModelKey", "model|id");
            Invoke(window, "ShowSettingsView");
            var settings = ((ContentControl)Get(window, "MainContent")).Content;
            SetCredentials(settings, "second-user", "second-password");
            Require((bool)Invoke(settings, "SaveAppSettings"), "Saving settings must succeed.");

            object secondProvider = Get(window, "authTokenProvider");
            object secondService = Get(window, "ruleService");
            var secondCookies = (CookieContainer)Get(window, "apiCookieContainer");
            Require(!ReferenceEquals(firstProvider, secondProvider), "Save must replace the cached token provider.");
            Require(!ReferenceEquals(firstService, secondService), "Save must replace the authorized service.");
            Require(!ReferenceEquals(firstCookies, secondCookies) && secondCookies.Count == 0, "Save must discard the old session cookies.");
            Require(((IList)Get(window, "rules")).Count == 0 && ((IList)Get(window, "validationRules")).Count == 0 && Get(window, "loadedRulesModelKey") == null,
                "Save must invalidate all cached validation rules.");
            Require((string)CallSettings("GetAuthUsername") == "second-user", "Saved username must become effective immediately.");
            Require((string)CallSettings("GetAuthPassword") == "second-password", "Saved encrypted password must become effective immediately.");
            ExpectInvalidSession(firstProvider);

            var secondHandler = AttachFakeTransport(secondProvider, secondService);
            Require(AuthenticatedUser(secondService) == "second-user", "First request after Save must authenticate as the new user.");
            object udpRequest = Activator.CreateInstance(addin.GetType("Veloxap.AddIn.Erwin.Models.GetCustomUdpRequest", true));
            ((Task)Invoke(secondService, "GetCustomUdp", udpRequest)).GetAwaiter().GetResult();
            Require(secondHandler.LastUserHeader == "second-user" && secondHandler.LastBearer == "token-second-user", "UDP username header and bearer token must identify the same new user.");
            Require(secondHandler.LoginCount == 1, "New session token should be reused after the first new login.");

            TestLateRules(window, settings, secondHandler);
            TestLateRulesFailure(window, settings);
            TestPendingLoginInvalidation(window, settings);

            SetCredentials(settings, "", "");
            Require((bool)Invoke(settings, "SaveAppSettings"), "Saving empty credentials must succeed.");
            Require((string)CallSettings("GetAuthUsername") == "" && !(bool)CallSettings("AreAuthCredentialsConfigured"),
                "Intentionally empty assembly credentials must not fall back to the old host user.");
            var config = ConfigurationManager.OpenExeConfiguration(assemblyPath);
            config.AppSettings.Settings.Remove("AuthUsername");
            config.Save(ConfigurationSaveMode.Modified);
            Require((string)CallSettings("GetAuthUsername") == "host-old-user", "Missing assembly keys must retain host fallback support.");
            Console.WriteLine("PASS: saved credentials, token and cookie replacement, UDP identity, rule-cache isolation, pending-login invalidation, host fallback.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return 1;
        }
    }

    private static void TestLateRules(object window, object settings, FakeTransport transport)
    {
        Set(window, "selectedModelName", "model");
        Set(window, "selectedModelLongId", "id");
        transport.PendingRules = new TaskCompletionSource<HttpResponseMessage>();
        SynchronizationContext.SetSynchronizationContext(null);
        var pending = (Task<bool>)Invoke(window, "LoadValidationRulesForSelectedModelAsync", false);
        Require(!pending.IsCompleted, "Old-user rules request must be pending during the switch.");
        SetCredentials(settings, "third-user", "third-password");
        Require((bool)Invoke(settings, "SaveAppSettings"), "Switch during pending rules must save.");
        transport.PendingRules.SetResult(Json("[{\"ruleText\":\"old-user-rule\"}]"));
        Require(!pending.GetAwaiter().GetResult(), "Old-user rules response must be discarded.");
        Require(((IList)Get(window, "rules")).Count == 0 && Get(window, "loadedRulesModelKey") == null,
            "A late old-user response must not repopulate the new user's cache.");
    }

    private static void TestPendingLoginInvalidation(object window, object settings)
    {
        object provider = Get(window, "authTokenProvider");
        Invoke(provider, "ClearToken");
        var transport = AttachFakeTransport(provider, Get(window, "ruleService"));
        transport.PendingLogin = new TaskCompletionSource<HttpResponseMessage>();
        var login = (Task<string>)Invoke(provider, "GetTokenAsync");
        Require(!login.IsCompleted, "Login must remain pending until the session changes.");
        SetCredentials(settings, "fifth-user", "fifth-password");
        Require((bool)Invoke(settings, "SaveAppSettings"), "Switch during pending login must save.");
        transport.PendingLogin.SetResult(Json("{\"token\":\"old-pending-token\"}"));
        bool rejected = false;
        try { login.GetAwaiter().GetResult(); }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected, "A login completing after session invalidation must not return the old token.");
        ExpectInvalidSession(provider);
    }

    private static void TestLateRulesFailure(object window, object settings)
    {
        var oldTransport = AttachFakeTransport(Get(window, "authTokenProvider"), Get(window, "ruleService"));
        oldTransport.PendingRules = new TaskCompletionSource<HttpResponseMessage>();
        var oldRequest = (Task<bool>)Invoke(window, "LoadValidationRulesForSelectedModelAsync", false);
        Require(!oldRequest.IsCompleted, "Old-user rules request must remain pending before the switch.");

        SetCredentials(settings, "fourth-user", "fourth-password");
        Require((bool)Invoke(settings, "SaveAppSettings"), "Switch during the old rules request must save.");
        var newTransport = AttachFakeTransport(Get(window, "authTokenProvider"), Get(window, "ruleService"));
        newTransport.RulesJson = "[{\"ruleText\":\"new-user-rule\"}]";
        Require(CompleteOnDispatcher(() => (Task<bool>)Invoke(window, "LoadValidationRulesForSelectedModelAsync", false)),
            "New user's rules request must succeed before the old request fails.");

        oldTransport.PendingRules.SetException(new InvalidOperationException("Delayed old-session failure"));
        Require(!oldRequest.GetAwaiter().GetResult(), "A late old-user failure must be ignored.");
        var rules = (IList)Get(window, "rules");
        Require(rules.Count == 1 && (string)rules[0] == "new-user-rule" && (string)Get(window, "loadedRulesModelKey") == "model|id",
            "An old-user failure must preserve the new user's populated rules cache.");
    }

    private static T CompleteOnDispatcher<T>(Func<Task<T>> start)
    {
        SynchronizationContext previous = SynchronizationContext.Current;
        var dispatcher = Dispatcher.CurrentDispatcher;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
        try
        {
            Task<T> task = start();
            if (!task.IsCompleted)
            {
                var frame = new DispatcherFrame();
                task.ContinueWith(ignored => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)));
                Dispatcher.PushFrame(frame);
            }
            return task.GetAwaiter().GetResult();
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }

    private static FakeTransport AttachFakeTransport(object provider, object service)
    {
        var transport = new FakeTransport();
        Set(provider, "httpClient", new HttpClient(transport, false));
        var handler = (DelegatingHandler)Activator.CreateInstance(addin.GetType("Veloxap.AddIn.Erwin.Services.BearerTokenHandler", true),
            new object[] { provider, transport });
        Set(service, "httpClient", new HttpClient(handler));
        return transport;
    }

    private static string AuthenticatedUser(object service)
    {
        return ((Task<string>)Invoke(service, "GetAuthenticatedUserNameAsync")).GetAwaiter().GetResult();
    }

    private static void ExpectInvalidSession(object provider)
    {
        bool rejected = false;
        try { ((Task<string>)Invoke(provider, "GetTokenAsync")).GetAwaiter().GetResult(); }
        catch (TargetInvocationException ex) { rejected = ex.InnerException is InvalidOperationException; }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected, "Retired providers must reject further requests.");
    }

    private static void SetCredentials(object settings, string username, string password)
    {
        ((TextBox)Get(settings, "txtAuthUsername")).Text = username;
        ((PasswordBox)Get(settings, "pwdAuthPassword")).Password = password;
    }

    private static object CallSettings(string method)
    {
        return addin.GetType("Veloxap.AddIn.Erwin.Services.RuleApiSettings", true).GetMethod(method, BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
    }

    private static object Invoke(object target, string method, params object[] args) { return target.GetType().GetMethod(method, Instance).Invoke(target, args); }
    private static object Get(object target, string field) { return target.GetType().GetField(field, Instance).GetValue(target); }
    private static void Set(object target, string field, object value) { target.GetType().GetField(field, Instance).SetValue(target, value); }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static HttpResponseMessage Json(string json) { return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) }; }

    private static void WriteAssemblySettings(string username, string password)
    {
        using (XmlWriter writer = XmlWriter.Create(assemblyPath + ".config", new XmlWriterSettings { Indent = true }))
        {
            writer.WriteStartElement("configuration");
            writer.WriteStartElement("appSettings");
            foreach (var pair in new Dictionary<string, string> {
                { "ApiBaseUrl", "https://session.invalid/" }, { "AuthLoginUrl", "auth/login" },
                { "AuthUsername", username }, { "AuthPassword", password },
                { "ValidationRulesByModelUrl", "rules" }, { "CustomUdpUrl", "custom-udp" }, { "AlterDdlUrl", "alter-ddl" }
            })
            {
                writer.WriteStartElement("add");
                writer.WriteAttributeString("key", pair.Key);
                writer.WriteAttributeString("value", pair.Value);
                writer.WriteEndElement();
            }
            writer.WriteEndElement();
            writer.WriteEndElement();
        }
    }

    private sealed class FakeTransport : HttpMessageHandler
    {
        public int LoginCount;
        public string LastUserHeader;
        public string LastBearer;
        public TaskCompletionSource<HttpResponseMessage> PendingRules;
        public TaskCompletionSource<HttpResponseMessage> PendingLogin;
        public string RulesJson = "[]";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri.AbsolutePath == "/auth/login")
            {
                LoginCount++;
                var payload = new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(await request.Content.ReadAsStringAsync());
                if (PendingLogin != null) return await PendingLogin.Task;
                return Json("{\"token\":\"token-" + payload["username"] + "\"}");
            }
            LastBearer = request.Headers.Authorization == null ? null : request.Headers.Authorization.Parameter;
            LastUserHeader = request.Headers.Contains("X-UserName") ? request.Headers.GetValues("X-UserName").Single() : null;
            if (request.RequestUri.AbsolutePath == "/auth/me")
                return Json("{\"success\":true,\"data\":{\"username\":\"" + LastBearer.Substring("token-".Length) + "\"}}");
            if (request.RequestUri.AbsolutePath == "/rules")
                return PendingRules == null ? Json(RulesJson) : await PendingRules.Task;
            return Json("{\"success\":true,\"data\":[]}");
        }
    }
}
