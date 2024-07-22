async function getChannels() {
  return new Promise((resolve, reject) => {
    var channelsRequest = new XMLHttpRequest();
    channelsRequest.onreadystatechange = (ev) => {
      if(ev.target.status == 200 && ev.target.response.length > 0)
        resolve(JSON.parse(ev.target.response))
    };
    channelsRequest.open("GET", "/channels");
    channelsRequest.send();
    // channelsRequest.setRequestHeader('Content-type', 'application/json; charset=utf-8');
    // channelsRequest.send(JSON.stringify({ID: localStorage.ID, Password: localStorage.Password}));
  });
}
let channels = {};
if ("WebSocket" in window) {
  var localStorage = window.localStorage;
  var ws = new WebSocket(`ws://${document.location.host}//WebSocket`);

  ws.onopen = async()=>{
    console.log("Start WebSocket")
    login()
    document.getElementById("user").textContent = localStorage.UserName;
    try{document.getElementById("channel-name").textContent = JSON.parse(localStorage.channel).Name;}catch(e){}
    document.getElementById("NotConnected").hidden = true;
    if(!localStorage.channel) {
      document.getElementById("NotChangedChannel").hidden = false;
    } else {
      document.getElementById("status-bar").hidden = false;
      document.getElementById("input").hidden = false;
      document.getElementById("Chat").hidden = false;
    }
    let rawchannels = await getChannels();
    console.log("Channels: ", rawchannels)
    rawchannels.forEach(channel => {
      pastChannel(channel.Name)
      channels[channel.Name] = channel;
    });
  }

  ws.onclose = (e)=>{
    console.log("WebSocket closed")
    console.log(e)
    pastMessage("Error", Date.now(), `Соединение с сервером потеряно`)
  }

  ws.onerror = (err) => { 
    pastMessage("Error", Date.now(), `Соединение с сервером потеряно`)
    console.log(err)
    throw err
  }

  ws.onmessage = (message)=>{
    console.log(message.data)
    let data = JSON.parse(message.data)
    data.Time = new Date(data.TimeStamp);
    console.log(data)
    switch(data.Type) {
      case "Text":
        pastMessage("Text", data.Time, data.Content)
        break;
      case "Info":
        pastMessage("Info", data.Time, data.Content)
        break;
      case "Error":
        if(data.Code == 400)
          pastMessage("Error", data.Time, `Ошибка в работе. <a href="${document.location.origin + "/Login.html"}">Войти</a>`)
        if(data.Code == 401)
          pastMessage("Error", data.Time, `Вы не вошли в систему. <a href="${document.location.origin + "/Login.html"}">Войти</a>`)
        if(data.Code == 429)
          pastMessage("Error", data.Time, `Вы шлете слишком много сообщений.`)
        if(data.Code == 413)
          pastMessage("Error", data.Time, `Ваше сообщение слишком длинное!`)
        break;
    }
  }
} else alert("ERROR\nYour browser is not supported!")

let MyID = localStorage.getItem("ID");

function login() {
  ws.send(JSON.stringify({
    Content: JSON.stringify({
      ID: MyID,
      Password: localStorage.getItem("Password")
    }),
    Author: MyID,
    Type: "Login",
    ChannelID: localStorage.channel && localStorage.channel != "undefined" ? JSON.parse(localStorage.channel).ID : "" 
  }))
}

function send(t) {
  console.log(((localStorage.channel && localStorage.channel != "undefined") ? localStorage.channel : "{\"ID\":\"\"}"))
  ws.send(JSON.stringify({
    Author: MyID,
    Content: t.value,
    Type: "Message",
    ChannelID: localStorage.channel && localStorage.channel != "undefined" ? JSON.parse(localStorage.channel).ID : "" 
  }))
  t.value = ""
}
function changeChannelByID(channelID) {
  ws.send(JSON.stringify({
    Content: channelID,
    Type: "ChangeChannel"
  }))
  chat.innerHTML = ""
}

let chat = document.getElementById("Chat")
let channelsList = document.getElementById("ChannelsList")
function pastMessage(type, time, msg) {
  if(!(time instanceof Date))time = new Date(time);
  chat.innerHTML =`<div class="Message ${type}"><span class="date">${!time ? "" : (time.toDateString()!=(new Date()).toDateString() ? time.toLocaleString() : time.toLocaleTimeString() ).slice(0,-3)}</span><span class="MessageContent">${msg}</span></div>` + chat.innerHTML
}
function pastChannel(name) {
  channelsList.innerHTML = `<button class="Channel" onclick="changeChannel(this);return false;">${name}</button>` + channelsList.innerHTML;
}
function changeChannel(button) {
  console.log(button)
  console.log(button.textContent)
  console.log(channels[button.textContent])
  localStorage.setItem("channel", JSON.stringify(channels[button.textContent]))
  changeChannelByID(channels[button.textContent].ID);
  document.getElementById("channel-name").textContent = button.textContent;
  document.getElementById("NotChangedChannel").hidden = true;
  document.getElementById("status-bar").hidden = false;
  document.getElementById("input").hidden = false;
  document.getElementById("Chat").hidden = false;
}