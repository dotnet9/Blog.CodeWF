namespace CodeWF.Web.ViewComponents;

public class FriendLinkViewComponent(ILogger<FriendLinkViewComponent> logger, IMediator mediator) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        try
        {
            IReadOnlyList<FriendLinkEntity>? links = await mediator.Send(new GetAllLinksQuery());
            return View(links ?? new List<FriendLinkEntity>());
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error Reading FriendLink.");
            return Content("ERROR");
        }
    }
}