Imports System.Net
Imports System.Net.Http
Imports System.Text
Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports System.Threading.Tasks
Imports System.Diagnostics
Imports System.IO

''' <summary>
''' Clase completa para gestionar OAuth2 con Google.
''' </summary>
Public Class GoogleAuthService

    ' Configuración de la API
    Private ReadOnly _clientId As String
    Private ReadOnly _clientSecret As String
    Private ReadOnly _scopes As String()
    Private ReadOnly _redirectUri As String = "http://127.0.0.1:55555/"

    ' Endpoints de Google
    Private Const AuthEndpoint As String = "https://accounts.google.com/o/oauth2/v2/auth"
    Private Const TokenEndpoint As String = "https://oauth2.googleapis.com/token"

    Public Sub New(clientId As String, clientSecret As String, scopes As String())
        _clientId = clientId
        _clientSecret = clientSecret
        _scopes = scopes
    End Sub

    ''' <summary>
    ''' Inicia el flujo de autenticación: abre el navegador, espera el código y obtiene los tokens.
    ''' </summary>
    Public Async Function AuthenticateAsync() As Task(Of GoogleToken)
        ' 1. Crear el HttpListener para esperar la respuesta de Google
        Using listener As New HttpListener()
            listener.Prefixes.Add(_redirectUri)
            listener.Start()

            ' 2. Generar URL de autorización
            Dim scopeString = String.Join(" ", _scopes)
            Dim authUrl = $"{AuthEndpoint}?response_type=code&client_id={_clientId}&redirect_uri={_redirectUri}&scope={Uri.EscapeDataString(scopeString)}&access_type=offline&prompt=consent"

            ' 3. Abrir el navegador predeterminado
            OpenBrowser(authUrl)

            ' 4. Esperar a que llegue la petición al puerto local
            Dim context = Await listener.GetContextAsync()
            Dim request = context.Request
            Dim response = context.Response

            ' 5. Extraer el código de la URL
            Dim code = request.QueryString("code")
            Dim errorString = request.QueryString("error")

            ' 6. Responder al navegador para cerrar la pestaña amigablemente
            Dim responseString As String
            If Not String.IsNullOrEmpty(code) Then
                responseString = "<html><body><h1>Autenticacion exitosa!</h1><p>Puedes cerrar esta ventana y volver a la aplicacion.</p><script>window.close();</script></body></html>"
            Else
                responseString = $"<html><body><h1>Error: {errorString}</h1></body></html>"
            End If

            Dim buffer = Encoding.UTF8.GetBytes(responseString)
            response.ContentLength64 = buffer.Length
            Dim output = response.OutputStream
            Await output.WriteAsync(buffer, 0, buffer.Length)
            output.Close()
            listener.Stop()

            If String.IsNullOrEmpty(code) Then
                Throw New Exception($"Error en autenticación: {errorString}")
            End If

            ' 7. Intercambiar el código por el Token
            Return Await ExchangeCodeForTokenAsync(code)
        End Using
    End Function

    ''' <summary>
    ''' Intercambia el código de autorización por un Access Token y Refresh Token.
    ''' </summary>
    Private Async Function ExchangeCodeForTokenAsync(code As String) As Task(Of GoogleToken)
        Using client As New HttpClient()
            Dim params As New Dictionary(Of String, String) From {
                {"code", code},
                {"client_id", _clientId},
                {"client_secret", _clientSecret},
                {"redirect_uri", _redirectUri},
                {"grant_type", "authorization_code"}
            }

            Dim content As New FormUrlEncodedContent(params)
            Dim response = Await client.PostAsync(TokenEndpoint, content)
            Dim jsonString = Await response.Content.ReadAsStringAsync()

            If Not response.IsSuccessStatusCode Then
                Throw New Exception($"Error obteniendo token: {jsonString}")
            End If

            Return JsonSerializer.Deserialize(Of GoogleToken)(jsonString)
        End Using
    End Function

    ''' <summary>
    ''' Usa el Refresh Token para obtener un nuevo Access Token cuando el anterior expira.
    ''' </summary>
    Public Async Function RefreshTokenAsync(refreshToken As String) As Task(Of GoogleToken)
        Using client As New HttpClient()
            Dim params As New Dictionary(Of String, String) From {
                {"client_id", _clientId},
                {"client_secret", _clientSecret},
                {"refresh_token", refreshToken},
                {"grant_type", "refresh_token"}
            }

            Dim content As New FormUrlEncodedContent(params)
            Dim response = Await client.PostAsync(TokenEndpoint, content)
            Dim jsonString = Await response.Content.ReadAsStringAsync()

            If Not response.IsSuccessStatusCode Then
                Throw New Exception($"Error refrescando token: {jsonString}")
            End If

            Dim newToken = JsonSerializer.Deserialize(Of GoogleToken)(jsonString)
            
            ' La respuesta de refresh a veces no devuelve un nuevo refresh_token, 
            ' así que mantenemos el viejo si el nuevo es nulo.
            If String.IsNullOrEmpty(newToken.RefreshToken) Then
                newToken.RefreshToken = refreshToken
            End If

            Return newToken
        End Using
    End Function

    ' Método auxiliar para abrir navegador compatible con .NET Core y Framework
    Private Sub OpenBrowser(url As String)
        Try
            Process.Start(New ProcessStartInfo(url) With {.UseShellExecute = True})
        Catch ex As Exception
            ' Fallback para algunos entornos de Windows
            Process.Start("cmd", $"/c start {url.Replace("&", "^&")}")
        End Try
    End Sub

End Class

''' <summary>
''' Estructura de datos para mapear la respuesta JSON de Google.
''' </summary>
Public Class GoogleToken
    <JsonPropertyName("access_token")>
    Public Property AccessToken As String

    <JsonPropertyName("expires_in")>
    Public Property ExpiresIn As Integer

    <JsonPropertyName("refresh_token")>
    Public Property RefreshToken As String

    <JsonPropertyName("scope")>
    Public Property Scope As String

    <JsonPropertyName("token_type")>
    Public Property TokenType As String

    Public ReadOnly Property CreatedDate As DateTime = DateTime.Now

    ' Propiedad auxiliar para saber si ha expirado (con margen de 1 minuto)
    Public ReadOnly Property IsExpired As Boolean
        Get
            Return DateTime.Now >= CreatedDate.AddSeconds(ExpiresIn - 60)
        End Get
    End Property
End Class
