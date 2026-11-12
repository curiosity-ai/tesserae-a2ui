using System;
using Tesserae;
using static Transpose.Core.dom;
using static Tesserae.UI;
using static Tesserae.A2UI.Sample.SamplesHelper;

namespace Tesserae.A2UI.Sample
{
    /// <summary>Every component of the basic catalog on one surface: the page the theme matrix is captured from.</summary>
    [SampleDetails(Group = "Guides", Order = 15, Icon = UIcons.LayoutFluid)]
    public class ShowcaseSample : IComponent, ISample
    {
        private readonly IComponent _content;

        public ShowcaseSample()
        {
            var host = new A2UIHost();

            host.Process(J(@"[{'version':'1.0','createSurface':{'surfaceId':'showcase','catalogId':'" + BASIC + @"','dataModel':{
                'name':'Ada Lovelace','bio':'Wrote the first program.','pin':'1234','qty':3,'agree':true,'level':40,'steps':2,'when':'2026-05-25T14:30:00','day':'2026-05-25','at':'09:15:00',
                'plan':['pro'],'tags':['a','c'],'topics':['ui'],'chips':['b'],
                'people':[{'n':'Grace','r':'Admiral'},{'n':'Alan','r':'Mathematician'},{'n':'Katherine','r':'Mathematician'}]},
              'components':[
                {'id':'root','component':'Column','children':['intro','grid']},
                {'id':'intro','component':'Text','text':'# Basic catalog\nEvery component of the A2UI basic catalog on one surface.'},
                {'id':'grid','component':'Row','align':'start','children':['c1','c2','c3']},

                {'id':'c1','component':'Column','weight':1,'children':['display_card','layout_card']},
                {'id':'c2','component':'Column','weight':1,'children':['inputs_card','choice_card']},
                {'id':'c3','component':'Column','weight':1,'children':['buttons_card','tabs_card','media_card']},

                {'id':'display_card','component':'Card','child':'display'},
                {'id':'display','component':'Column','children':['t_body','t_caption','t_md','icons','img_row']},
                {'id':'t_body','component':'Text','text':'**Text** with *Markdown*, `code` and a list:\n\n- one\n- two'},
                {'id':'t_caption','component':'Text','variant':'caption','text':'A caption'},
                {'id':'t_md','component':'Text','text':{'@call':'formatString','args':{'value':'${formatCurrency(value: 1234.5, currency: \'EUR\')} · ${formatNumber(value: 98765)} · ${pluralize(value: ${/qty}, one: \'item\', other: \'items\')}'}}},
                {'id':'icons','component':'Row','children':['i1','i2','i3','i4','i5']},
                {'id':'i1','component':'Icon','name':'home'},{'id':'i2','component':'Icon','name':'star'},{'id':'i3','component':'Icon','name':'mail'},{'id':'i4','component':'Icon','name':'settings'},
                {'id':'i5','component':'Icon','name':{'svgPath':'M12 2 L22 22 L2 22 Z'}},
                {'id':'img_row','component':'Row','align':'center','children':['img_a','img_b']},
                {'id':'img_a','component':'Image','variant':'avatar','url':'data:image/svg+xml,%3Csvg xmlns=%27http://www.w3.org/2000/svg%27 viewBox=%270 0 40 40%27%3E%3Crect width=%2740%27 height=%2740%27 fill=%27%23888%27/%3E%3Ccircle cx=%2720%27 cy=%2715%27 r=%278%27 fill=%27%23ddd%27/%3E%3Crect x=%278%27 y=%2727%27 width=%2724%27 height=%2713%27 rx=%276%27 fill=%27%23ddd%27/%3E%3C/svg%3E','description':'avatar'},
                {'id':'img_b','component':'Image','variant':'icon','url':'data:image/svg+xml,%3Csvg xmlns=%27http://www.w3.org/2000/svg%27 viewBox=%270 0 40 40%27%3E%3Crect width=%2740%27 height=%2740%27 fill=%27%23888%27/%3E%3Ccircle cx=%2720%27 cy=%2715%27 r=%278%27 fill=%27%23ddd%27/%3E%3Crect x=%278%27 y=%2727%27 width=%2724%27 height=%2713%27 rx=%276%27 fill=%27%23ddd%27/%3E%3C/svg%3E','description':'icon'},

                {'id':'layout_card','component':'Card','child':'layout'},
                {'id':'layout','component':'Column','children':['row_demo','div_h','list_demo']},
                {'id':'row_demo','component':'Row','justify':'spaceBetween','align':'center','children':['rd1','div_v','rd2']},
                {'id':'rd1','component':'Text','text':'left'},{'id':'rd2','component':'Text','text':'right'},
                {'id':'div_v','component':'Divider','axis':'vertical'},
                {'id':'div_h','component':'Divider'},
                {'id':'list_demo','component':'List','children':{'componentId':'person','path':'/people'}},
                {'id':'person','component':'Row','justify':'spaceBetween','children':['p_n','p_r']},
                {'id':'p_n','component':'Text','text':{'@path':'n'}},
                {'id':'p_r','component':'Text','variant':'caption','text':{'@path':'r'}},

                {'id':'inputs_card','component':'Card','child':'inputs'},
                {'id':'inputs','component':'Column','children':['f_short','f_long','f_num','f_pw','f_checked','f_valid','dt_both','dt_date','dt_time','slider','slider_steps']},
                {'id':'f_short','component':'TextField','label':'Name','value':{'@path':'/name'},'placeholder':'Your name'},
                {'id':'f_long','component':'TextField','label':'Bio','variant':'longText','value':{'@path':'/bio'}},
                {'id':'f_num','component':'TextField','label':'Quantity','variant':'number','value':{'@path':'/qty'}},
                {'id':'f_pw','component':'TextField','label':'PIN','variant':'obscured','value':{'@path':'/pin'}},
                {'id':'f_checked','component':'CheckBox','label':'I agree','value':{'@path':'/agree'}},
                {'id':'f_valid','component':'TextField','label':'Required, with a check','value':'','@checks':[{'condition':{'@call':'required','args':{'value':{'@path':'/nothing'}}},'message':'This one always fails'}]},
                {'id':'dt_both','component':'DateTimeInput','label':'Date and time','enableDate':true,'enableTime':true,'value':{'@path':'/when'}},
                {'id':'dt_date','component':'DateTimeInput','label':'Date','enableDate':true,'value':{'@path':'/day'}},
                {'id':'dt_time','component':'DateTimeInput','label':'Time','enableTime':true,'value':{'@path':'/at'}},
                {'id':'slider','component':'Slider','label':'Level','min':0,'max':100,'value':{'@path':'/level'}},
                {'id':'slider_steps','component':'Slider','label':'In steps','min':0,'max':4,'steps':4,'value':{'@path':'/steps'}},

                {'id':'choice_card','component':'Card','child':'choices'},
                {'id':'choices','component':'Column','children':['ch_radio','ch_multi','ch_chips','ch_filter']},
                {'id':'ch_radio','component':'ChoicePicker','label':'Plan (one)','options':[{'label':'Free','value':'free'},{'label':'Pro','value':'pro'},{'label':'Team','value':'team'}],'value':{'@path':'/plan'}},
                {'id':'ch_multi','component':'ChoicePicker','label':'Tags (several)','variant':'multipleSelection','options':[{'label':'Alpha','value':'a'},{'label':'Beta','value':'b'},{'label':'Gamma','value':'c'}],'value':{'@path':'/tags'}},
                {'id':'ch_chips','component':'ChoicePicker','label':'Chips','displayStyle':'chips','options':[{'label':'One','value':'a'},{'label':'Two','value':'b'},{'label':'Three','value':'c'}],'value':{'@path':'/chips'}},
                {'id':'ch_filter','component':'ChoicePicker','label':'Filterable','variant':'multipleSelection','filterable':true,'options':[{'label':'Agents','value':'ag'},{'label':'Catalogs','value':'ca'},{'label':'Surfaces','value':'su'},{'label':'Themes','value':'th'}],'value':{'@path':'/topics'}},

                {'id':'buttons_card','component':'Card','child':'buttons'},
                {'id':'buttons','component':'Column','children':['buttons_row','b_off']},
                {'id':'buttons_row','component':'Row','children':['b_def','b_pri','b_bor','b_icon']},
                {'id':'b_def_l','component':'Text','text':'Default'},{'id':'b_def','component':'Button','child':'b_def_l','action':{'event':{'name':'default'}}},
                {'id':'b_pri_l','component':'Text','text':'Primary'},{'id':'b_pri','component':'Button','variant':'primary','child':'b_pri_l','action':{'event':{'name':'primary'}}},
                {'id':'b_bor_l','component':'Text','text':'Borderless'},{'id':'b_bor','component':'Button','variant':'borderless','child':'b_bor_l','action':{'event':{'name':'borderless'}}},
                {'id':'b_icon_i','component':'Icon','name':'send'},{'id':'b_icon','component':'Button','child':'b_icon_i','action':{'event':{'name':'icon'}}},
                {'id':'b_off_l','component':'Text','text':'Disabled by a check'},{'id':'b_off','component':'Button','child':'b_off_l','action':{'event':{'name':'never'}},'@checks':[{'condition':false,'message':'Always disabled'}]},

                {'id':'tabs_card','component':'Card','child':'tabs'},
                {'id':'tabs','component':'Tabs','tabs':[{'title':'First','child':'tab1'},{'title':'Second','child':'tab2'}]},
                {'id':'tab1','component':'Text','text':'The **first** tab'},{'id':'tab2','component':'Text','text':'The *second* tab'},

                {'id':'media_card','component':'Card','child':'media'},
                {'id':'media','component':'Column','children':['video','audio','modal']},
                {'id':'video','component':'Video','url':'assets/media/sample-video.webm'},
                {'id':'audio','component':'AudioPlayer','url':'assets/media/sample-audio.wav','description':'A song'},
                {'id':'modal','component':'Modal','trigger':'modal_open','content':'modal_body'},
                {'id':'modal_open_l','component':'Text','text':'Open a modal'},{'id':'modal_open','component':'Button','child':'modal_open_l','action':{'event':{'name':'open'}}},
                {'id':'modal_body','component':'Column','children':['mb1','mb2']},{'id':'mb1','component':'Text','text':'## A modal'},{'id':'mb2','component':'Text','text':'Its content is any component.'}
              ]}}]"));

            _content = Guide.Page("Component showcase", UIcons.LayoutFluid, "Every basic-catalog component on one surface", "ShowcaseSample.cs",
"All eighteen components of the A2UI basic catalog, each drawn with the Tesserae component that does the job: **Text** (Markdown), **Image**, **Icon**, **Row**, **Column**, **List**, **Card**, **Tabs**, **Divider**, **Modal**, **Button**, **CheckBox**, **TextField** (short, long, number, obscured), **DateTimeInput** (date, time, both), **ChoicePicker** (radio, checkbox, chips, filterable), **Slider**, and the two media components, **Video** and **AudioPlayer**. This page is what the theme matrix of the test suite captures.",
                Card(VStack().WS().Padding(8.px()).Children(host.WS())).SetTitle("Rendered by Tesserae.A2UI"));
        }

        public HTMLElement Render() => _content.Render();
    }
}
