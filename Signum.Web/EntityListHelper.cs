﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Mvc;
using System.Collections;
using System.Linq.Expressions;
using System.Web.Mvc.Html;
using Signum.Entities;
using Signum.Entities.Reflection;
using Signum.Utilities;
using System.Configuration;

namespace Signum.Web
{
    public static class EntityListKeys
    {
        public const string Index = "sfIndex";
    }

    public class EntityList : EntityBase
    {
        public Type EntitiesType { get; set; }
        public string DetailDiv = null;

        public EntityList()
        {
        }

        public override void SetReadOnly()
        {
            Find = false;
            Create = false;
            Remove = false;
            Implementations = null;
        }
    }

    public static class EntityListHelper
    {
        private static void InternalEntityList<T>(this HtmlHelper helper, string idValueField, MList<T> value, EntityList settings, TypeContext<MList<T>> typeContext)
        //    where T : Modifiable
        {
            if (!settings.Visible)
                return;

            idValueField = helper.GlobalName(idValueField);
            string divASustituir = helper.GlobalName("divASustituir");

            StringBuilder sb = new StringBuilder();
            
            Type elementsCleanType = Reflector.ExtractLazy(typeof(T)) ?? typeof(T);
            
            sb.Append(helper.Hidden(idValueField + TypeContext.Separator + TypeContext.StaticType, elementsCleanType.Name) + "\n");

            if (StyleContext.Current.LabelVisible)
                sb.Append(helper.Span(idValueField + "lbl", settings.LabelText ?? "", TypeContext.CssLineLabel));

            if (settings.ShowFieldDiv)
                sb.Append("<div class='fieldList'>");

            string popupOpeningParameters = "'{0}','{1}','{2}',function(){{OnListPopupOK('{3}','{2}',this.id);}},function(){{OnListPopupCancel(this.id);}}".Formato("Signum/PopupView", divASustituir, idValueField, "Signum/ValidatePartial");

            if (settings.Implementations != null) //Interface with several possible implementations
            {
                sb.Append("<div id=\"" + idValueField + TypeContext.Separator + EntityBaseKeys.Implementations + "\" name=\"" + idValueField + TypeContext.Separator + EntityBaseKeys.Implementations + "\" style=\"display:none\" >\n");

                //List<SelectListItem> types = new List<SelectListItem> { new SelectListItem { Text = "Select type", Value = "", Selected = true } };
                string strButtons = "";
                foreach (Type t in settings.Implementations)
                {
                    strButtons += "<input type='button' id='{0}' name='{0}' value='{1}' /><br />\n".Formato(t.Name, Navigator.TypesToURLNames.TryGetC(t) ?? t.Name);
                }
                //string ddlStr = helper.DropDownList(idValueField + TypeContext.Separator + EntityBaseKeys.ImplementationsDDL, types);
                sb.Append(helper.RenderPartialToString(
                    "~/Plugin/Signum.Web.dll/Signum.Web.Views.OKCancelPopup.ascx",
                    new ViewDataDictionary(value) 
                        { 
                            { ViewDataKeys.CustomHtml, strButtons},
                            { ViewDataKeys.PopupPrefix, idValueField},
                        }
                ));
                sb.Append("</div>\n");
            }

            string viewingUrl = "OpenPopupList(" + popupOpeningParameters + ",'{0}');".Formato(settings.DetailDiv);
            StringBuilder sbSelect = new StringBuilder();
            sbSelect.Append("<select id=\"{0}\" name=\"{0}\" multiple=\"multiple\" ondblclick=\"{1}\" class=\"entityList\">\n".Formato(idValueField, viewingUrl));

            if (value != null)
            {
                for (int i = 0; i < value.Count; i++)
                {
                    sb.Append(InternalListElement(helper, sbSelect, idValueField, value[i], i, settings, divASustituir, typeContext));
                }
            }

            sbSelect.Append("</select>\n");

            sb.Append(sbSelect);

            StringBuilder sbBtns = new StringBuilder();

            if (settings.Create)
            {
                string creatingUrl = (settings.Implementations == null) ?
                    "NewPopupList({0},'{1}','{2}','{3}');".Formato(popupOpeningParameters, elementsCleanType.Name, typeof(EmbeddedEntity).IsAssignableFrom(elementsCleanType), settings.DetailDiv) :
                    "$('#{0} :button').each(function(){{".Formato(idValueField + TypeContext.Separator + EntityBaseKeys.Implementations) +
                            "$('#' + this.id).unbind('click').click(function(){" +
                                "OnListImplementationsOk({0},'{1}','{2}',this.id);".Formato(popupOpeningParameters, typeof(EmbeddedEntity).IsAssignableFrom(elementsCleanType), settings.DetailDiv) +
                            "});" +
                        "});" +
                        ((settings.Implementations.Count() == 1) ? 
                            "$('#{0} :button').click();".Formato(idValueField + TypeContext.Separator + EntityBaseKeys.Implementations) :
                            "ChooseImplementation('{0}','{1}',function(){{}},function(){{OnImplementationsCancel('{1}');}});".Formato(divASustituir, idValueField));

                sbBtns.AppendLine("<tr><td>");
                sbBtns.Append(
                        helper.Button(idValueField + "_btnCreate",
                                  "+",
                                  creatingUrl,
                                  "lineButton create",
                                  new Dictionary<string, object>()));
                sbBtns.AppendLine("</td></tr>");
            }

            if (settings.Find && !typeof(EmbeddedEntity).IsAssignableFrom(elementsCleanType))
            {
                    string popupFindingParameters = "'{0}','{1}','true',function(){{OnListSearchOk('{2}','{3}');}},function(){{OnListSearchCancel('{2}','{3}');}},'{3}','{2}'".Formato("Signum/PartialFind", Navigator.TypesToURLNames.TryGetC(Reflector.ExtractLazy(typeof(T)) ?? typeof(T)), idValueField, divASustituir);
                    string findingUrl = (settings.Implementations == null) ?
                        "Find({0});".Formato(popupFindingParameters) :
                        "$('#{0} :button').each(function(){{".Formato(idValueField + TypeContext.Separator + EntityBaseKeys.Implementations) +
                            "$('#' + this.id).unbind('click').click(function(){" +
                                "OnSearchImplementationsOk({0},this.id);".Formato(popupFindingParameters) +
                            "});" +
                        "});" +
                        ((settings.Implementations.Count() == 1) ? 
                            "$('#{0} :button').click();".Formato(idValueField + TypeContext.Separator + EntityBaseKeys.Implementations) :
                            "ChooseImplementation('{0}','{1}',function(){{}},function(){{OnImplementationsCancel('{1}');}});".Formato(divASustituir, idValueField));

                    sbBtns.AppendLine("<tr><td>");
                    sbBtns.Append(
                            helper.Button(idValueField + "_btnFind",
                                        "O",
                                        findingUrl,
                                        "lineButton find",
                                        new Dictionary<string, object>()));
                    sbBtns.AppendLine("</td></tr>");
            }
            
            if (settings.Remove)
            {
                sbBtns.AppendLine("<tr><td>");
                sbBtns.Append(
                        helper.Button(idValueField + "_btnRemove",
                                  "x",
                                  "RemoveListContainedEntity('{0}');".Formato(idValueField),
                                  "lineButton remove",
                                  (value == null || value.Count == 0) ? new Dictionary<string, object>() { { "style", "display:none" } } : new Dictionary<string, object>()));
                sbBtns.AppendLine("</td></tr>");
            }
            
            string sBtns = sbBtns.ToString();
            if (sBtns.HasText())
                sb.AppendLine("<table>\n" + sBtns + "</table>\n");

            if (settings.ShowFieldDiv)
                sb.Append("</div>");
            if (StyleContext.Current.BreakLine)
                sb.Append("<div class=\"clearall\"></div>\n");

            helper.ViewContext.HttpContext.Response.Write(sb.ToString());
        }

        private static string InternalListElement<T>(this HtmlHelper helper, StringBuilder sbOptions, string idValueField, T value, int index, EntityList settings, string divASustituir, TypeContext<MList<T>> typeContext)
        {
            StringBuilder sb = new StringBuilder();
            
            bool isIdentifiable = typeof(IdentifiableEntity).IsAssignableFrom(typeof(T));
            bool isLazy = typeof(Lazy).IsAssignableFrom(typeof(T));

            string indexedPrefix = idValueField + TypeContext.Separator + index.ToString() + TypeContext.Separator;

            string runtimeType = "";
            if (value != null)
            {
                Type cleanRuntimeType = value.GetType();
                if (typeof(Lazy).IsAssignableFrom(value.GetType()))
                    cleanRuntimeType = (value as Lazy).RuntimeType;
                runtimeType = cleanRuntimeType.Name;
            }
            sb.Append(helper.Hidden(indexedPrefix + TypeContext.RuntimeType, runtimeType) + "\n");
            sb.Append(helper.Hidden(indexedPrefix + EntityListKeys.Index, index.ToString()) + "\n");

            if (isIdentifiable || isLazy)
            {
                sb.Append(helper.Hidden(
                    indexedPrefix + TypeContext.Id,
                    (isIdentifiable)
                       ? ((IIdentifiable)(object)value).TryCS(i => i.IdOrNull)
                       : ((Lazy)(object)value).TryCS(i => i.IdOrNull)) + "\n");

                sb.Append(helper.Div(indexedPrefix + EntityBaseKeys.Entity, "", "", new Dictionary<string, object> { { "style", "display:none" } }));
                
                if (isIdentifiable &&((IIdentifiable)(object)value).TryCS(i => i.IdOrNull) == null)
                    sb.Append(helper.Hidden(indexedPrefix + EntityBaseKeys.IsNew, index.ToString()) + "\n");
            
                //Note this is added to the sbOptions, not to the result sb
                sbOptions.Append("<option id=\"" + indexedPrefix + EntityBaseKeys.ToStr + "\" " +
                                "name=\"" + indexedPrefix + EntityBaseKeys.ToStr + "\" " + 
                                "value=\"\" " +
                                "class = \"valueLine entityListOption\" " +
                                ">" + 
                                ((isIdentifiable)
                                    ? ((IdentifiableEntity)(object)value).TryCC(i => i.ToString())
                                    : ((Lazy)(object)value).TryCC(i => i.ToStr)) + 
                                "</option>\n");
            }
            else
            {
                //It's an embedded entity: Render popupcontrol with embedded entity to the _sfEntity hidden div
                sb.Append("<div id=\"" + indexedPrefix + EntityBaseKeys.Entity + "\" name=\"" + indexedPrefix + EntityBaseKeys.Entity + "\" style=\"display:none\" >\n");

                EntitySettings es = Navigator.Manager.EntitySettings.TryGetC(typeof(T)).ThrowIfNullC("No hay una vista asociada al tipo: " + typeof(T));

                TypeElementContext<T> tsc = new TypeElementContext<T>(value, typeContext, index);

                using (var sc = StyleContext.RegisterCleanStyleContext(true))
                    sb.Append(
                        helper.RenderPartialToString(
                            "~/Plugin/Signum.Web.dll/Signum.Web.Views.PopupControl.ascx",
                            new ViewDataDictionary(tsc)  //value instead of tsc
                            { 
                                { ViewDataKeys.MainControlUrl, es.PartialViewName},
                                { ViewDataKeys.PopupPrefix, idValueField + TypeContext.Separator + index.ToString()}
                            }
                        )
                    );

                sb.Append("</div>\n");

                //Note this is added to the sbOptions, not to the result sb
                sbOptions.Append("<option id=\"" + indexedPrefix + EntityBaseKeys.ToStr + "\" " +
                                "name=\"" + indexedPrefix + EntityBaseKeys.ToStr + "\" " +
                                "value=\"\" " +
                                "class = valueLine\" " +
                                ">" +
                                ((EmbeddedEntity)(object)value).TryCC(i => i.ToString()) + 
                                "</option>\n");
            }

            sb.Append("<script type=\"text/javascript\">var " + indexedPrefix + "sfEntityTemp = \"\"</script>\n");

            return sb.ToString();
        }

        public static void EntityList<T, S>(this HtmlHelper helper, TypeContext<T> tc, Expression<Func<T, MList<S>>> property)
        //    where S : Modifiable 
        {
            TypeContext<MList<S>> context = Common.WalkExpression(tc, property);

            Type entitiesType = typeof(T);

            EntityList el = new EntityList() { EntitiesType = entitiesType };
            
            //if (el.Implementations == null)
                Navigator.ConfigureEntityBase(el, Reflector.ExtractLazy(typeof(S)) ?? typeof(S), false);

            Common.FireCommonTasks(el, Reflector.ExtractLazy(entitiesType) ?? entitiesType, context);

            helper.InternalEntityList<S>(context.Name, context.Value, el, context);
        }

        public static void EntityList<T, S>(this HtmlHelper helper, TypeContext<T> tc, Expression<Func<T, MList<S>>> property, Action<EntityList> settingsModifier)
        //    where S : Modifiable
        {
            TypeContext<MList<S>> context = Common.WalkExpression(tc, property);

            Type entitiesType = typeof(T);

            EntityList el = new EntityList() { EntitiesType = entitiesType };
            
            //if (el.Implementations == null)
                Navigator.ConfigureEntityBase(el, Reflector.ExtractLazy(typeof(S)) ?? typeof(S), false);

            Common.FireCommonTasks(el, Reflector.ExtractLazy(entitiesType) ?? entitiesType, context);

            settingsModifier(el);
                        
            //if (el.StyleContext != null)
            //{
            //    using (el.StyleContext)
            //        helper.InternalEntityList<S>(context.Name, context.Value, el);
            //    return;
            //}

            if (el != null)
                using (el)
                    helper.InternalEntityList<S>(context.Name, context.Value, el, context);
            else
                helper.InternalEntityList<S>(context.Name, context.Value, el, context);
        }
    }
}
