using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Nop.Core.Infrastructure;

namespace CW.Common.Helpers;

public static class HttpContextHelper
{
    /// <summary>
    /// Gets the current HTTP Context
    /// </summary>
    /// <returns></returns>
    public static HttpContext GetCurrentContext()
    {
        var httpContextAccessor = EngineContext.Current.Resolve<IHttpContextAccessor>();
        return httpContextAccessor.HttpContext;
    }

    /// <summary>
    /// Checks if the current HTTP Request has a querystring parameter with the given name
    /// </summary>
    /// <param name="paramName">The parameter name</param>
    /// <param name="httpContext">The current HttpContext</param>
    /// <returns>True if the parameter exists in the querystring, false otherwise</returns>
    public static bool HasQuerystringParam(string paramName, HttpContext httpContext)
    {
        string param = httpContext.Request.Query[paramName];
        return (param != null);
    }

    /// <summary>
    /// Checks if the current HTTP Request has a querystring parameter with the given name and value
    /// </summary>
    /// <param name="paramName">The parameter name</param>
    /// <param name="paramValue">The parameter value</param>
    /// <param name="httpContext">The current HttpContext</param>
    /// <returns>True if the parameter exists in the querystring and also has the given value, false otherwise</returns>
    public static bool HasQuerystringParam(string paramName, string paramValue, HttpContext httpContext)
    {
        string param = httpContext.Request.Query[paramName];

        return (param != null && param.Equals(paramValue));
    }

    /// <summary>
    /// Gets a parameter from the current querystring as a string value
    /// </summary>
    /// <param name="paramName">The parameter name</param>
    /// <param name="httpContext">The current HttpContext</param>
    /// <returns>Returns the given parameter as a string value or null if not found</returns>
    public static string GetStringQuerystringParam(string paramName, HttpContext httpContext)
    {
        return httpContext.Request.Query[paramName];
    }

    /// <summary>
    /// Gets a parameter from the current querystring as an integer value
    /// </summary>
    /// <param name="paramName">The parameter name</param>
    /// <param name="httpContext">The current HttpContext</param>
    /// <returns>
    /// Returns the given parameter as a nullable integer value or null if not found or
    /// if the value cannot be properly converted to an integer
    /// </returns>
    public static int? GetIntegerQuerystringParam(string paramName, HttpContext httpContext)
    {
        string param = GetStringQuerystringParam(paramName, httpContext);
        if (param != null)
        {
            try
            {
                return Int32.Parse(param);
            }
            catch (Exception)
            {
                return null;
            }
        }
        return null;
    }

    /// <summary>
    /// Gets a given parameter from the current HTTP context request, that is values that are coming as part
    /// of the request.
    /// </summary>
    /// <param name="paramName">The parameter name</param>
    /// <param name="httpContext">The current HTTP context</param>
    /// <returns>The parameter value as a string or null if not found</returns>
    public static string GetRequestParam(string paramName, HttpContext httpContext)
    {
        // First place, look on the Querystring
        string param = GetStringQuerystringParam(paramName, httpContext);
        if (string.IsNullOrEmpty(param))
        {
            // Second place, look on Forms variables
            param = httpContext.Request.Form[paramName];
            if (string.IsNullOrEmpty(param))
            {
                // Third place, look on Cookies
                param = httpContext.Request.Cookies[paramName];
            }
        }
        // Return whatever we found
        return paramName;
    }

    /// <summary>
    /// Determines if the current HTTP request is a POST
    /// </summary>
    /// <param name="httpContext">The current HTTP context</param>
    /// <returns>True if the request is a POST, false otherwise</returns>
    public static bool IsPost(HttpContext httpContext)
    {
        return httpContext.Request.Method == "POST";
    }

    /// <summary>
    /// Determines if the current HTTP request is a GET
    /// </summary>
    /// <param name="httpContext">The current HTTP context</param>
    /// <returns>True if the request is a GET, false otherwise</returns>
    public static bool IsGet(HttpContext httpContext)
    {
        return httpContext.Request.Method == "GET";
    }

    /// <summary>
    /// Adds a given string value to the current HTTP session
    /// </summary>
    /// <param name="key">The value key on the session</param>
    /// <param name="value">The value to add</param>
    /// <param name="httpContext">The current HTTP context</param>
    public static void AddSessionValue(string key, string value, HttpContext httpContext)
    {
        httpContext.Session.SetString(key, value);
    }

    /// <summary>
    /// Adds a given int value to the current HTTP session
    /// </summary>
    /// <param name="key">The value key on the session</param>
    /// <param name="value">The value to add</param>
    /// <param name="httpContext">The current HTTP context</param>
    public static void AddSessionValue(string key, int value, HttpContext httpContext)
    {
        httpContext.Session.SetInt32(key, value);
    }

    /// <summary>
    /// Removes a given session value from the current session
    /// </summary>
    /// <param name="key">The session key that identifies the value</param>
    /// <param name="httpContext">The current HTTP context</param>
    public static void ClearSessionValue(string key, HttpContext httpContext)
    {
        httpContext.Session.Remove(key);
    }

    /// <summary>
    /// Obtains a session string value from the current HTTP session
    /// </summary>
    /// <param name="key">The key that identifies the session value</param>
    /// <param name="httpContext">The HTTP context</param>
    /// <returns>The session value or null if not found</returns>
    public static string GetSessionStringValue(string key, HttpContext httpContext)
    {
        return httpContext.Session.GetString(key);
    }

    /// <summary>
    /// Obtains a session int value from the current HTTP session
    /// </summary>
    /// <param name="key">The key that identifies the session value</param>
    /// <param name="httpContext">The HTTP context</param>
    /// <returns>The session value or null if not found</returns>
    public static int? GetSessionIntValue(string key, HttpContext httpContext)
    {
        return httpContext.Session.GetInt32(key);
    }

    /// <summary>
    /// Extension method to check if request is AJAX based
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    public static bool IsAjaxRequest(this HttpRequest request)
    {
        if (request == null)
            throw new ArgumentNullException("request");

        if (request.Headers != null)
            return request.Headers["X-Requested-With"] == "XMLHttpRequest";
        return false;
    }

    /// <summary>
    /// Determines if the current request is Ajax
    /// </summary>
    /// <param name="httpContext">The current HTTP context</param>
    /// <returns><code>true</code> if the request is ajax or <code>false</code> otherwise</returns>
    public static bool IsAjaxRequest(HttpContext httpContext)
    {
        return httpContext.Request.IsAjaxRequest();
    }

    /// <summary>
    /// Determines if the current request is Ajax
    /// </summary>
    /// <returns></returns>
    public static bool IsAjaxRequest()
    {
        var httpContext = GetCurrentContext();
        return httpContext.Request.IsAjaxRequest();
    }

    /// <summary>
    /// Determines if a HTTP request is available on the given HTTP Context
    /// </summary>
    /// <param name="httpContext"></param>
    /// <returns><code>true</code> if a HTTP request is available, <code>false</code> otherwise</returns>
    public static Boolean IsRequestAvailable(HttpContext httpContext)
    {
        if (httpContext == null)
            return false;

        try
        {
            if (httpContext.Request == null)
                return false;
        }
        catch (Exception)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Adds the <code>BackToStore</code> key to the current HTTP context session based on the presence of the
    /// querystring parameter <code>backToStore</code> so other action filters can identify if it is necessary to 
    /// redirect back to the Store Dashboard
    /// </summary>
    /// <param name="httpContext">The current HTTP context</param>
    public static void AddBackToStoreFlag(HttpContext httpContext)
    {
        int? storeId = GetIntegerQuerystringParam("backToStore", httpContext);
        if (storeId != null && storeId.HasValue)
        {
            // Add a session flag so we can keep track in other action filters
            AddSessionValue("BackToStore", storeId.Value, httpContext);
        }
    }

    /// <summary>
    /// Cleans up the <code>BackToStore</code> flag value from the current HTTP session
    /// </summary>
    /// <param name="httpContext">The current HTTP context</param>
    public static void RemoveBackToStoreFlag(HttpContext httpContext)
    {
        ClearSessionValue("BackToStore", httpContext);
    }

    /// <summary>
    /// Obtains the value of the <code>BackToStore</code> session key which represents the ID of the
    /// Store to which the request should be redirected when a redirect to a Store is needed
    /// </summary>
    /// <param name="httpContext">The current HTTP context</param>
    /// <returns>The flag value (an integer represeting the Store ID) or null of not found</returns>
    public static int? GetBackToStoreFlagValue(HttpContext httpContext)
    {
        return GetSessionIntValue("BackToStore", httpContext);
    }

    /// <summary>
    /// Determines if it is necessary to redirect to the Store Dashboard based on the presence of the
    /// <code>BackToStore</code> flag on the current HTTP session
    /// </summary>
    /// <param name="httpContext">The current HTTP context</param>
    /// <returns>
    ///     <code>true</code> if a redirection to the Store Dashboard should occur, 
    ///     <code>false</code> otherwise
    /// </returns>
    public static bool ShouldGoBackToStore(HttpContext httpContext)
    {
        return GetBackToStoreFlagValue(httpContext) != null;
    }

    /// <summary>
    /// Checks if the current HTTP Request has a form value with the given name
    /// </summary>
    /// <param name="formValueName">The form value name</param>
    /// <param name="httpContext">The current HttpContext</param>
    /// <returns>True if the form value exists in the request, false otherwise</returns>
    public static bool HasFormValue(string formValueName, HttpContext httpContext)
    {
        return httpContext.Request.Form.Keys.Any(x => x.Equals(formValueName, StringComparison.InvariantCultureIgnoreCase));
    }

    /// <summary>
    /// Gets the current Action Context
    /// </summary>
    /// <returns></returns>
    public static ActionContext GetCurrentActionContext()
    {
        var actionContextAccessor = EngineContext.Current.Resolve<IActionContextAccessor>();
        if (actionContextAccessor != null)
        {
            return actionContextAccessor.ActionContext;
        }
        throw new InvalidOperationException("Cannot determine current Action Context");
    }

    /// <summary>
    /// Gets the controller name based on the provided <code>ActionContext</code>
    /// </summary>
    /// <param name="actionContext">The Action Context</param>
    /// <returns>The name of the controller associated to the given action context</returns>
    public static string GetControllerName(ActionContext actionContext)
    {
        return actionContext.RouteData.Values["controller"].ToString();
    }

    /// <summary>
    /// Gets the action name based on the provided <code>ActionContext</code>
    /// </summary>
    /// <param name="actionContext">The Action Context</param>
    /// <returns>The name of the action associated to the given action context</returns>
    public static string GetActionName(ActionContext actionContext)
    {
        return actionContext.RouteData.Values["action"].ToString();
    }


    /// <summary>
    /// Determines if the specified controller name is the current controller name
    /// </summary>
    /// <param name="controllerName">The controller name</param>
    /// <param name="actionContext">
    /// An ActionContext which will be used to determine the current controller, if it is 
    /// null the current action content will be determined by using <code>IActionContextAccessor</code>
    /// </param>
    /// <returns>true if the condition is met, false otherwise</returns>
    public static bool IsController(string controllerName, ActionContext actionContext = null)
    {
        var currentActionContext = actionContext ?? GetCurrentActionContext();
        var currentControllerName = GetControllerName(currentActionContext);
        return controllerName.ToLowerInvariant() == currentControllerName.ToLowerInvariant();
    }

    /// <summary>
    /// Determines if the specified action name is the current controller action name based on the provided Action Context and optionally the
    /// provided controller name. If the controller name is provided then both the action and controller must match, if only the action is
    /// provided then only the action must match
    /// </summary>
    /// <param name="actionName">The action name</param>
    /// <param name="actionContext">The Action Context</param>
    /// <param name="controllerName">The controller name</param>
    /// <returns>true if the condition is met, false otherwise</returns>
    public static bool IsAction(string actionName, ActionContext actionContext = null, string controllerName = null)
    {
        var currentActionContext = actionContext ?? GetCurrentActionContext();
        var currentActionName = GetActionName(currentActionContext);
        if (!string.IsNullOrEmpty(controllerName))
        {
            var currentControllerName = GetControllerName(currentActionContext);
            return (actionName.ToLowerInvariant() == currentActionName.ToLowerInvariant()) &&
                (controllerName.ToLowerInvariant() == currentControllerName.ToLowerInvariant());
        }
        else
        {
            return actionName.ToLowerInvariant() == currentActionName.ToLowerInvariant();
        }
    }

    /// <summary>
    /// Tells if the current page context is the Shopping Cart page
    /// </summary
    /// <param name="httpContext">The current HTTP Context</param>
    /// <returns>true if current page is the Shopping Cart page, false otherwise</returns>
    public static bool IsShoppingCartContext()
    {
        return IsController("shoppingcart");
    }

    /// <summary>
    /// Returns the name of the MVC Area for the given route data or null if no area is defined
    /// </summary>
    /// <param name="routeData"></param>
    /// <returns></returns>
    public static string GetMVCArea(RouteData routeData)
    {
        if (routeData.DataTokens.ContainsKey("area"))
        {
            return routeData.DataTokens["area"].ToString();
        }
        return null;
    }

    /// <summary>
    /// Determines if the given route data is within the NOP Admin area
    /// </summary>
    /// <param name="routeData"></param>
    /// <returns></returns>
    public static bool IsNOPAdminArea(RouteData routeData)
    {
        string areaToken = GetMVCArea(routeData);
        if (areaToken != null)
        {
            return areaToken.Equals("Admin", StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    /// <summary>
    /// Determines if the given HTTP context belongs to the Admin area
    /// </summary>
    /// <param name="httpContext"></param>
    /// <returns></returns>
    public static bool IsAdminArea(HttpContext httpContext)
    {
        if (httpContext != null)
        {
            var request = httpContext.Request;
            if (request != null)
            {
                string path = request.Path;
                var pathLowercase = path.ToLower();
                return pathLowercase.StartsWith("/admin/");
            }
        }
        return false;
    }

    /// <summary>
    /// Determines if the given HTTP context belongs to the webhook
    /// </summary>
    /// <param name="httpContext"></param>
    /// <returns></returns>
    public static bool IsWebhook(HttpContext httpContext)
    {
        if (httpContext != null)
        {
            var request = httpContext.Request;
            if (request != null)
            {
                string path = request.Path;
                var pathLowercase = path.ToLower();
                return pathLowercase.StartsWith("/webhook/");
            }
        }
        return false;
    }

    /// <summary>
    /// Determines if current context is within the admin area by using the given httcontext and route data
    /// </summary>
    /// <param name="httpContext"></param>
    /// <param name="routeData"></param>
    /// <returns></returns>
    public static bool IsAdminAreaContext(HttpContext httpContext, RouteData routeData)
    {
        return IsNOPAdminArea(routeData) || IsAdminArea(httpContext);
    }

    /// <summary>
    /// Determines if current context is within the webhook by using the given httcontext and route data
    /// </summary>
    /// <param name="httpContext"></param>
    /// <param name="routeData"></param>
    /// <returns></returns>
    public static bool IsWebhookContext(HttpContext httpContext)
    {
        return IsWebhook(httpContext);
    }
}