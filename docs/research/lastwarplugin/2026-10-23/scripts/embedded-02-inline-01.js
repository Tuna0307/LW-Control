
var LANGS=['vi','en','zh','tw','ko','ru','ja','ar','pt'];
var T={
 subtitle:['Chọn hồ sơ game và khởi chạy','Select a game profile and launch','选择游戏配置并启动','選擇遊戲配置並啟動','게임 프로필을 선택하고 실행','Выберите профиль и запустите','ゲームプロファイルを選択して起動','اختر ملف تعريف اللعبة وابدأ التشغيل','Selecione um perfil e inicie'],
 validLabel:['Thời hạn còn','Remaining validity','剩余有效期','剩餘有效期','남은 기간','Осталось','残り有効期間','المدة المتبقية','Validade restante'],
 days:['ngày','days','天','天','일','дн.','日','يوم','dias'],
 mwLabel:['Đa cửa sổ','Multi-open','多开','多開','다중 실행','Мультиокно','マルチ起動','فتح متعدد','Multi-abertura'],
 mwLabelReg:['Slot đăng ký / đã mua','Registered / purchased','注册/已购','註冊/已購','등록/구매','Зарег./куплено','登録/購入','مسجّل/مشترى','Registrado/comprado'],
 mwLabelOpen:['Cửa sổ đã mở / đăng ký','Opened / registered','已开/注册','已開/註冊','열림/등록','Открыто/зарег.','起動中/登録','مفتوح/مسجّل','Aberto/registrado'],
 slot:['slot','slots','个','個','슬롯','слот','スロット','فتحة','slots'],
 pathLabel:['Đường dẫn game','Game path','游戏路径','遊戲路徑','게임 경로','Путь к игре','ゲームパス','مسار اللعبة','Caminho do jogo'],
 browseBtn:['Chọn đường dẫn…','Browse…','浏览…','瀏覽…','찾아보기…','Обзор…','参照…','استعراض…','Procurar…'],
 profilesTitle:['Hồ sơ game','Game profiles','游戏配置','遊戲配置','게임 프로필','Профили игры','ゲームプロファイル','ملفات اللعبة','Perfis de jogo'],
 profilesSub:['Nháy đúp một hồ sơ để khởi chạy','Double-click a profile to launch','双击配置以启动','雙擊配置以啟動','프로필을 더블클릭하여 실행','Двойной клик по профилю — запуск','ダブルクリックで起動','انقر مرتين على ملف للتشغيل','Clique duplo para iniciar'],
 saveBtn:['+ Lưu cấu hình','+ Save configuration','+ 保存配置','+ 儲存配置','+ 구성 저장','+ Сохранить','+ 構成を保存','+ حفظ الإعداد','+ Salvar configuração'],
 addTile:['Lưu hồ sơ mới','Save new profile','保存新配置','儲存新配置','새 프로필 저장','Новый профиль','新規プロファイル','حفظ ملف جديد','Salvar novo perfil'],
 refreshBtn:['↻ Làm mới','↻ Refresh','↻ 刷新','↻ 重新整理','↻ 새로고침','↻ Обновить','↻ 更新','↻ تحديث','↻ Atualizar'],
 launchBtn:['Khởi Chạy','Launch','启动','啟動','실행','Запустить','起動','تشغيل','Iniciar'],
 launching:['Đang kết nối…','Connecting…','连接中…','連線中…','연결 중…','Подключение…','接続中…','جارٍ الاتصال…','Conectando…'],
 menuModeLb:['Kiểu menu (chuột phải):','Menu type (right-click):','菜单类型(右键):','選單類型(右鍵):','메뉴 유형(우클릭):','Тип меню (ПКМ):','メニュー種別(右クリック):','نوع القائمة (نقر يمين):','Tipo de menu (clique direito):'],
 mmOverlay:['Overlay in-game','In-game overlay','游戏内悬浮','遊戲內懸浮','인게임 오버레이','Оверлей в игре','ゲーム内オーバーレイ','تراكب داخل اللعبة','Sobreposição no jogo'],
 mmWebview:['Cửa sổ webview','Webview window','Webview 窗口','Webview 視窗','웹뷰 창','Окно Webview','Webviewウィンドウ','نافذة Webview','Janela Webview'],
 currentTag:['Đang mở','Open','已打开','已開啟','열림','Открыто','開いています','مفتوح','Aberto'],
 lastUsed:['Dùng lần cuối','Last used','上次使用','上次使用','마지막 사용','Последний вход','最終使用','آخر استخدام','Último uso'],
 never:['Chưa','Never','从未','從未','없음','Никогда','なし','أبداً','Nunca'],
 noProfName:['Đăng nhập không hồ sơ','No-profile login','无配置登录','無配置登入','프로필 없이 로그인','Вход без профиля','プロファイルなしログイン','تسجيل دخول بدون ملف','Login sem perfil'],
 noProfDesc:['Xoá trạng thái đăng nhập hiện tại','Clear current login state','清除当前登录状态','清除目前登入狀態','현재 로그인 상태 지우기','Сбросить текущий вход','現在のログイン状態をクリア','مسح حالة تسجيل الدخول','Limpar estado de login'],
 empty:['Chưa có hồ sơ','No profiles yet','暂无配置','暫無配置','프로필 없음','Нет профилей','プロファイルなし','لا توجد ملفات','Nenhum perfil'],
 stOff:['Chưa kết nối','Not connected','未连接','未連線','연결 안 됨','Не подключено','未接続','غير متصل','Não conectado'],
 stIng:['Đang kết nối','Connecting','连接中','連線中','연결 중','Подключение','接続中','جارٍ الاتصال','Conectando'],
 stOn:['Đã kết nối','Connected','已连接','已連線','연결됨','Подключено','接続済み','متصل','Conectado'],
 delTitle:['Xóa hồ sơ','Delete profile','删除配置','刪除配置','프로필 삭제','Удалить профиль','プロファイル削除','حذف الملف','Excluir perfil'],
 delConfirm:['Xoá hồ sơ này?','Delete this profile?','删除此配置？','刪除此配置？','이 프로필을 삭제할까요?','Удалить этот профиль?','このプロファイルを削除しますか？','حذف هذا الملف؟','Excluir este perfil?'],
 signedIn:['Đã đăng nhập','Signed in','已登录','已登入','로그인됨','Выполнен вход','サインイン済み','تم تسجيل الدخول','Conectado']
};
var _lang='vi', _li=0, _data={}, _sel=null, _status={}, _widx={};   // _status[uid]='off'|'ing'|'on' · _widx[uid]=slot cửa sổ
// Rust gọi để cập nhật trạng thái kết nối 1 hồ sơ (no connect/connecting/connected).
var _launching=false, _launchTimer=null, _preOn=null;   // disable nút Khởi Chạy tới khi window VỪA MỞ kết nối ổn định ('on')
function anyConnecting(){   // có window nào ĐANG kết nối (đã mở, chưa verify+fire)?
  for(var k in _status){ if(_status[k]==='ing') return true; }
  return false;
}
function updateLaunchBtn(){
  var b=document.getElementById('launchBtn'); if(!b) return;
  // KHOÁ nút khi: vừa bấm Launch (_launching) HOẶC còn window đang 'connecting' (user 2026-08-25:
  //   "đang connecting thì Launch phải disable, connected mới nhả ra để mở profile 2"). Trạng thái 'ing'
  //   đến từ mmap realtime (pid sống + ACT!=1) nên tự hết khi window kết nối xong HOẶC bị đóng.
  var busy = _launching || anyConnecting();
  b.disabled=busy; b.classList.toggle('busy',busy);
  b.textContent=busy?tr('launching'):tr('launchBtn');
}
function beginLaunching(){
  // Chụp các window ĐANG 'on' trước khi mở window mới → chỉ mở lại nút khi có window MỚI đạt 'on'
  //   (nếu không, status 'on' của window cũ sẽ mở nút sớm → user bấm mở profile 2 khi profile 1 chưa kết nối).
  _preOn={}; for(var k in _status){ if(_status[k]==='on') _preOn[k]=1; }
  _launching=true; updateLaunchBtn();
  if(_launchTimer) clearTimeout(_launchTimer);
  _launchTimer=setTimeout(function(){ _launching=false; updateLaunchBtn(); },90000);   // fallback: 90s không kết nối → mở lại
}
function endLaunching(){ _launching=false; if(_launchTimer){clearTimeout(_launchTimer);_launchTimer=null;} updateLaunchBtn(); }
window.lwSetStatus=function(id,st,idx){ if(!id) return; _status[id]=st;
  // idx = SLOT CỬA SỔ do core cấp (first-free-slot), KHÔNG gắn với profile: đóng window 1 rồi mở cái
  // khác thì cái đó lại là 1. Hiện lên tile để biết profile nào đang ở cửa sổ nào (probe/IPC theo idx này).
  if(st==='off') delete _widx[id]; else if(idx>0) _widx[id]=idx;
  if(st==='on'&&_launching&&!(_preOn&&_preOn[id])) endLaunching();   // window MỚI 'on' → bỏ cờ _launching
  updateLaunchBtn();   // 'ing' xuất hiện/biến mất cũng phải khoá/nhả nút
  render(); };
function tr(k){return (T[k]&&T[k][_li])||(T[k]&&T[k][1])||k;}
function esc(s){return (s==null?'':String(s)).replace(/[&<>]/g,function(c){return{'&':'&amp;','<':'&lt;','>':'&gt;'}[c];});}
var _menuMode='overlay';   // menu chuột phải mỗi window: 'overlay' (ImGui in-game) | 'webview' (cửa sổ ui.html). Đổi được, gửi kèm launch.
function setMenuMode(m){
  _menuMode=(m==='webview')?'webview':'overlay';
  document.querySelectorAll('#menuMode .mm-opt').forEach(function(b){b.classList.toggle('mm-sel', b.getAttribute('data-mm')===_menuMode);});
}
function lwRefresh(){
  // Nút LÀM MỚI: gọi 'save' (Rust re-fetch /api/profiles + re-render lwInit) + feedback quay nhẹ.
  var b=document.getElementById('refreshBtn');
  if(b){ b.disabled=true; b.style.opacity='0.6'; setTimeout(function(){ if(b){b.disabled=false; b.style.opacity='';} },1200); }
  lw('save');
}
function lw(action,extra){
  var prof=_sel;
  if(action==='launch'){
    // tile 'none' = CHỦ ĐỘNG tạo acc mới → luôn '__new__' (game về màn login trong sandbox).
    if(_sel==='none') prof='__new__';
    else if(!_sel) prof='';   // chưa chọn gì → '' → core seed='import' (dùng acc đang login trên máy)
    // '__import__' = acc đang login sẵn trên máy → core copy vào sandbox 1 lần (giữ session, KHÔNG login lại).
    //   Giữ nguyên chuỗi để core map sang seed='import'. KHÔNG còn tile 'main' (tool luôn chạy sandbox).
    // tile hồ sơ có thật: profile = uid → core restore đúng account
    // KHÔNG tự đặt trạng thái ở JS (user 2026-08-25): trạng thái chỉ có MỘT nguồn = launcher đọc mmap realtime
    //   (pid sống + ACT). JS tự set 'ing' → nếu window chết trước khi core publish thì KHÔNG ai gỡ ⇒ tile kẹt
    //   "Connecting" vĩnh viễn dù không có game. Nút Launch vẫn khoá qua `_launching` (beginLaunching).
  }
  if(action==='launch'){
    if(_launching) return;   // đang mở window trước, chưa kết nối xong → chặn bấm dồn
    extra=Object.assign({menu_mode:_menuMode},extra||{});   // kèm menu-mode chọn cho window
    beginLaunching();
  }
  try{window.ipc.postMessage(JSON.stringify(Object.assign({action:action,profile:prof},extra||{})));}catch(e){}
}

function setLang(lang,save){
  _lang=lang; _li=LANGS.indexOf(lang); if(_li<0){_li=1;_lang='en';}
  document.documentElement.lang=_lang;
  document.documentElement.setAttribute('dir', _lang==='ar'?'rtl':'ltr');
  document.getElementById('langSel').value=_lang;
  document.querySelectorAll('[data-i]').forEach(function(el){el.textContent=tr(el.getAttribute('data-i'));});
  document.querySelector('.mhead .s').textContent=tr('profilesSub');
  render(); updateLaunchBtn();   // giữ text nút Khởi Chạy đúng trạng thái (launching) sau khi đổi ngôn ngữ
  if(save) lw('setlang',{lang:_lang});
}
function render(){
  document.getElementById('validDays').innerHTML=(_data.validDays==null?'—':_data.validDays)+' <span class="u">'+tr('days')+'</span>';
  var mwMax=(_data.mwMax||5), mwReg=(_data.mwUsed||0);   // đã mua=mwMax, đăng ký=mwUsed(uid bind)
  var mwOpen=Object.keys(_status).filter(function(k){return _status[k]==='on';}).length;   // đã mở = window đang kết nối (live)
  document.getElementById('mwStatReg').innerHTML=mwReg+' <span class="u">/ '+mwMax+' '+tr('slot')+'</span>';
  document.getElementById('mwStatOpen').innerHTML=mwOpen+' <span class="u">/ '+mwReg+' '+tr('slot')+'</span>';
  if(_data.gamePath) document.getElementById('gamePath').textContent=_data.gamePath;
  document.getElementById('signed').textContent=_data.signed||tr('signedIn');
  var pl=document.getElementById('plan'); var free=String(_data.plan||'premium').toLowerCase()!=='premium';
  pl.textContent=free?'FREE':'PREMIUM'; pl.className='plan'+(free?' free':'');
  var all=(_data.profiles||[]);
  // '__import__' = LỐI VÀO "nhập acc đang login trên máy" (chưa nằm trong sổ) → KHÔNG tính vào quota slot,
  //   giống tile 'none'. Chỉ acc THẬT trong sổ mới chiếm slot.
  var real=all.filter(function(p){return p.id!=='__import__';});
  var list=all.slice();
  var el=document.getElementById('grid'); el.innerHTML='';
  // MẶC ĐỊNH CHỌN = hồ sơ ĐẦU (account chính) → Khởi Chạy restore an toàn, KHÔNG vô tình clear.
  //   No-profile (clear→login acc mới) CHỈ khi user CHỦ ĐỘNG click. Chưa có hồ sơ nào → mặc định no-profile (giữ acc đang login).
  if(!_sel) _sel = real.length ? real[0].id : (all.length ? all[0].id : 'none');
  // tile "không hồ sơ" (thêm guest mới) — CHỈ khi CÒN slot (real.length < quota mua). Hết slot → ẩn.
  var _quota=(_data.mwMax||99);
  if(real.length < _quota && !list.some(function(p){return p.id==='none';}))
    list.unshift({id:'none',name:tr('noProfName'),desc:tr('noProfDesc'),lastUsed:tr('never'),current:(_sel==='none')});
  list.forEach(function(p){
    var isNone=(p.id==='none');
    var isMain=(p.id==='__import__');                    // tile 'nhập acc máy' (chưa có trong sổ) → không xoá
    if(isNone){p.name=tr('noProfName');p.desc=tr('noProfDesc');}
    var t=document.createElement('div');
    t.className='tile'+((p.id===_sel)?' sel':'');       // sel = HIGHLIGHT chọn (border), KHÔNG phải "đang mở"
    var st=_status[p.id]||'off';                        // off | ing | on
    var stTxt=tr(st==='on'?'stOn':(st==='ing'?'stIng':'stOff'));
    var isOpen=(st==='on'||st==='ing');                 // "đang mở" = window đã spawn (connecting/connected)
    var h='';
    if(!isNone && !isMain) h+='<button class="del" title="'+tr('delTitle')+'">&times;</button>';
    h+='<div class="nm">'+esc(p.name)+(isOpen?' <span class="tag">'+tr('currentTag')+'</span>':'')+'</div>';
    var wi=_widx[p.id];
    if(!isNone) h+='<div class="st st-'+st+'"><span class="dot"></span>'+stTxt+
      (isOpen&&wi?' <span class="widx">Index: '+wi+'</span>':'')+'</div>';   // tên ở .nm; server/lv/nạp ở .de
    h+='<div class="de">'+esc(p.desc||'')+'</div>'+
       '<div class="lu">'+tr('lastUsed')+': '+esc(p.lastUsed||tr('never'))+'</div>';
    t.innerHTML=h;
    t.onclick=function(){_sel=p.id;document.querySelectorAll('.tile').forEach(function(x){x.classList.remove('sel');});t.classList.add('sel');};
    t.ondblclick=function(){_sel=p.id;lw('launch');};
    if(!isNone){var db=t.querySelector('.del'); if(db){
      // XÓA 2 BƯỚC (chống click/double-click NHẦM nút × khi định Launch → mất profile). Click 1 = ARM (× → ?),
      //   click 2 trong 3s mới xoá thật. confirm() native bị WebView2 chặn nên KHÔNG dùng.
      db.onclick=function(ev){ev.stopPropagation();
        var now=Date.now();
        // Click 2 CỐ Ý (>400ms sau click 1) mới xoá → double-click (2 click <400ms) CHỈ arm, KHÔNG xoá.
        if(db._armed && (now-(db._armedAt||0))>400){ lw('delete',{profile:p.id}); return; }
        db._armed=true; db._armedAt=now; db.textContent='?'; db.title=tr('delConfirm'); db.style.background='rgba(255,90,90,.75)'; db.style.color='#fff';
        setTimeout(function(){ db._armed=false; db.innerHTML='&times;'; db.title=tr('delTitle'); db.style.background=''; db.style.color=''; },3000);
      };
    }}
    el.appendChild(t);
  });
  // Bỏ tile "Lưu hồ sơ mới": hồ sơ TỰ LƯU sau khi login guest (KHÔNG lưu thủ công).
}
function lwInit(d){
  _data=d||{};
  if(_data.title)document.getElementById('title').textContent=_data.title;
  setLang(_data.lang||'vi',false);   // setLang gọi render()
}
/*__LW_INIT__*/
