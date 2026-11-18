using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RaceDay.Web.Models;
using RaceDay.Web.Controllers;

namespace RaceDay.Web.Tests;

public sealed class OrganiserEventFormTests
{
    [Fact]
    public void EventCreateAndEditPostsKeepAntiforgeryAndUploadSizeLimits()
    {
        var create = typeof(OrganiserController).GetMethod(nameof(OrganiserController.CreateEvent),
            [typeof(EventEditModel), typeof(CancellationToken)]);
        var edit = typeof(OrganiserController).GetMethod(nameof(OrganiserController.EditEvent),
            [typeof(int), typeof(EventEditModel), typeof(CancellationToken)]);

        Assert.NotNull(create);
        Assert.NotNull(edit);
        Assert.NotNull(create!.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), inherit: true).SingleOrDefault());
        Assert.NotNull(edit!.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), inherit: true).SingleOrDefault());
        Assert.Equal(EventEditModel.MaximumBannerFileSizeBytes + 64 * 1024,
            ((IRequestSizeLimitMetadata)create.GetCustomAttributes(typeof(RequestSizeLimitAttribute), true)
                .Cast<RequestSizeLimitAttribute>().Single()).MaxRequestBodySize);
        Assert.Equal(EventEditModel.MaximumBannerFileSizeBytes,
            edit.GetCustomAttributes(typeof(RequestFormLimitsAttribute), true).Cast<RequestFormLimitsAttribute>().Single().MultipartBodyLengthLimit);
        Assert.Equal(typeof(IFormFile), typeof(EventEditModel).GetProperty(nameof(EventEditModel.BannerFile))!.PropertyType);
    }

    [Fact]
    public void EventDistanceInputRendersInvariantValueAndRangeForSouthAfricanCulture()
    {
        using var culture = UseCulture("en-ZA");
        var html = RenderDistanceInput(0.01m);

        Assert.Contains("type=\"number\"", html);
        Assert.Contains("value=\"0.01\"", html);
        Assert.Contains("min=\"0.01\"", html);
        Assert.Contains("max=\"999.99\"", html);
        Assert.Contains("step=\"0.01\"", html);
        Assert.Contains("data-val-range-min=\"0.01\"", html);
        Assert.Contains("data-val-range-max=\"999.99\"", html);
        Assert.Contains("name=\"__Invariant\"", html);
    }

    [Theory]
    [InlineData(0.01, true)]
    [InlineData(999.99, true)]
    [InlineData(0.00, false)]
    [InlineData(1000.00, false)]
    public void EventDistanceValidationUsesInvariantLimitsUnderSouthAfricanCulture(decimal distance, bool expectedValid)
    {
        using var culture = UseCulture("en-ZA");
        var model = new EventEditModel { Distance = distance };
        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateProperty(
            model.Distance,
            new ValidationContext(model) { MemberName = nameof(EventEditModel.Distance) },
            validationResults);

        Assert.Equal(expectedValid, isValid);
    }

    [Fact]
    public void CategoryDistanceValidationUsesInvariantLimitsUnderSouthAfricanCulture()
    {
        using var culture = UseCulture("en-ZA");
        var model = new CategoryEditModel { Distance = 0.01m };
        var validationResults = new List<ValidationResult>();

        var isValid = Validator.TryValidateProperty(
            model.Distance,
            new ValidationContext(model) { MemberName = nameof(CategoryEditModel.Distance) },
            validationResults);

        Assert.True(isValid);
    }

    private static string RenderDistanceInput(decimal distance)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMvcCore().AddViews().AddDataAnnotations();
        using var provider = services.BuildServiceProvider();

        var model = new EventFormViewModel { Event = new EventEditModel { Distance = distance } };
        var metadataProvider = provider.GetRequiredService<IModelMetadataProvider>();
        var viewData = new ViewDataDictionary<EventFormViewModel>(metadataProvider, new ModelStateDictionary())
        {
            Model = model
        };
        var modelExpressionProvider = provider.GetRequiredService<IModelExpressionProvider>();
        var expression = modelExpressionProvider.CreateModelExpression(viewData, item => item.Event.Distance);
        var httpContext = new DefaultHttpContext { RequestServices = provider };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var viewContext = new ViewContext(
            actionContext,
            new EmptyView(),
            viewData,
            new TempDataDictionary(httpContext, new EmptyTempDataProvider()),
            TextWriter.Null,
            new HtmlHelperOptions());

        var tagHelper = new InputTagHelper(provider.GetRequiredService<IHtmlGenerator>())
        {
            For = expression,
            InputTypeName = "number",
            ViewContext = viewContext
        };
        var attributes = new TagHelperAttributeList
        {
            { "type", "number" },
            { "min", "0.01" },
            { "max", "999.99" },
            { "step", "0.01" }
        };
        var output = new TagHelperOutput("input", attributes,
            (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()))
        {
            TagMode = TagMode.SelfClosing
        };
        var context = new TagHelperContext(attributes, new Dictionary<object, object>(), "Event.Distance");

        tagHelper.Process(context, output);
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        output.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    private static IDisposable UseCulture(string cultureName)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        var previousUiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);
        return new CultureScope(previousCulture, previousUiCulture);
    }

    private sealed class CultureScope(CultureInfo culture, CultureInfo uiCulture) : IDisposable
    {
        public void Dispose()
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
        }
    }

    private sealed class EmptyView : IView
    {
        public string Path => string.Empty;
        public Task RenderAsync(ViewContext context) => Task.CompletedTask;
    }

    private sealed class EmptyTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
