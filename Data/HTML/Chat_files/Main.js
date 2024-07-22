if ("WebSocket" in window) {
  var localStorage = window.localStorage;
  var ws = new WebSocket(`ws://${document.location.host}//WebSocket`);

  ws.onopen = ()=>{
    login()
    console.log("Start WebSocket")
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
    data.Time = new Date(data.Time);
    console.log(data)
    switch(data.Type) {
      case "Text":
        pastMessage("Message", data.Time, data.Content)
        break;
      case "Info":
        pastMessage("Info", data.Time, data.Content)
        break;
      case "Error":
        if(data.Code == 400)
          pastMessage("Error", data.Time, `Ошибка в работе. <a href="${document.location.origin + "/Login.html"}">Войти</a>`)
        if(data.Code == 401)
          pastMessage("antiquewhite", data.Time, `Вы не вошли в систему. <a href="${document.location.origin + "/Login.html"}">Войти</a>`)
        if(data.Code == 429)
          pastMessage("antiquewhite", data.Time, `Вы шлете слишком много сообщений.`)
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
    Type: "Login"
  }))
}

function send(t) {
  ws.send(JSON.stringify({
    Author: MyID,
    Content: t.value,
    Type: "Message"
  }))
  t.value = ""
}

let chat = document.getElementById("Chat")
function pastMessage(type, time, msg) {
  chat.innerHTML =`<div class="${type}"><spawn class="date">${(time.toDateString()!=(new Date()).toDateString() ? time.toLocaleString() : time.toLocaleTimeString() ).slice(0,-3)}</span>: ${msg}</div>` + chat.innerHTML
}