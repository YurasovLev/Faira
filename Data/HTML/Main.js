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
    pastMessage("Lightblue", `Соединение с сервером потеряно`)
  }

  ws.onerror = (err) => { 
    console.log(err)
    throw err
  }

  ws.onmessage = (message)=>{
    console.log(message.data)
    let data = JSON.parse(message.data)
    console.log(data)
    switch(data.Type) {
      case "Text":
        pastMessage("mistyrose", data.Content)
        break;
      case "Info":
        pastMessage("white", data.Content)
        break;
      case "Error":
        if(data.Code == 400)
          pastMessage("antiquewhite", `Ошибка в работе. <a href="${document.location.origin + "/Login.html"}">Войти</a>`)
        if(data.Code == 401)
          pastMessage("antiquewhite", `Вы не вошли в систему. <a href="${document.location.origin + "/Login.html"}">Войти</a>`)
        if(data.Code == 429)
          pastMessage("antiquewhite", `Вы шлете слишком много сообщений, потому вы отключены. <a href="${document.location.href}">Перезагрузить страницу</a>.`)
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
function pastMessage(color, msg) {
  chat.innerHTML =`<div style="color:${color}">${msg}</div>` + chat.innerHTML
}