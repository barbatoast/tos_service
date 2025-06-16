port module Main exposing (main)

import Browser
import Html exposing (Html, div, text, input, button)
import Html.Attributes exposing (..)
import Html.Events exposing (onInput, onClick)
import Http
import Json.Encode as Encode
import Json.Decode as Decode


-- PORTS

port saveToken : String -> Cmd msg
port loadToken : () -> Cmd msg
port sendToken : String -> Cmd msg
port receiveToken : (String -> msg) -> Sub msg
port clearToken : () -> Cmd msg


-- MODEL

type alias Model =
    { username : String
    , password : String
    , token : Maybe String
    , message : String
    }


init : () -> ( Model, Cmd Msg )
init _ =
    ( { username = "", password = "", token = Nothing, message = "" }
    , loadToken ()
    )


-- MESSAGES

type Msg
    = SetUsername String
    | SetPassword String
    | SubmitLogin
    | GotLogin (Result Http.Error String)
    | Logout
    | ReceiveStoredToken String
    | MakeProtectedRequest
    | GotProtectedResponse (Result Http.Error String)

fetchProtectedResource : String -> Cmd Msg
fetchProtectedResource token =
    Http.request
        { method = "GET"
        , headers = [ Http.header "Authorization" ("Bearer " ++ token) ]
        , url = "http://127.0.0.1:5000/users"
        , body = Http.emptyBody
        , expect = Http.expectString GotProtectedResponse
        , timeout = Nothing
        , tracker = Nothing
        }


subscriptions : Model -> Sub Msg
subscriptions _ =
    receiveToken ReceiveStoredToken


-- UPDATE

update : Msg -> Model -> ( Model, Cmd Msg )
update msg model =
    case msg of
        SetUsername u -> ( { model | username = u }, Cmd.none )

        SetPassword p -> ( { model | password = p }, Cmd.none )

        SubmitLogin ->
            ( model, loginRequest model.username model.password )

        GotLogin (Ok token) ->
            ( { model | token = Just token, message = "Login OK" }
            , saveToken token
            )

        GotLogin (Err err) ->
            ( { model | message = "Login failed: " ++ Debug.toString err }, Cmd.none )

        Logout ->
            ( { model | token = Nothing, message = "Logged out" }, clearToken () )

        MakeProtectedRequest ->
            case model.token of
                Just token ->
                    ( model, fetchProtectedResource token )

                Nothing ->
                    ( { model | message = "Not logged in" }, Cmd.none )

        ReceiveStoredToken tokenStr ->
            let
                token =
                    if String.isEmpty tokenStr then
                        Nothing
                    else
                        Just tokenStr
            in
            ( { model | token = token, message = if token /= Nothing then "Restored token" else "No token in storage" }
            , Cmd.none
            )

        GotProtectedResponse (Ok body) ->
            ( { model | message = "Protected call success: " ++ body }, Cmd.none )

        GotProtectedResponse (Err _) ->
            ( { model | message = "Protected call failed." }, Cmd.none )


-- VIEW

view : Model -> Html Msg
view model =
    div []
        [ input [ placeholder "Username", value model.username, onInput SetUsername ] []
        , input [ placeholder "Password", type_ "password", value model.password, onInput SetPassword ] []
        , button [ onClick SubmitLogin ] [ text "Login" ]

        , case model.token of
            Just token ->
                div []
                    [ div [] [ text ("✅ Token found: " ++ token) ]
                    , button [ onClick MakeProtectedRequest ] [ text "Make Protected API Call" ]
                    , button [ onClick Logout ] [ text "Logout" ]
                    ]
            Nothing ->
                div [] [ text "🔒 Not logged in." ]

        , div [] [ text model.message ]
        ]


-- HTTP

loginRequest : String -> String -> Cmd Msg
loginRequest u p =
    Http.post
        { url = "http://127.0.0.1:5000/login"
        , body = Http.jsonBody <| Encode.object [ ("username", Encode.string u), ("password", Encode.string p) ]
        , expect = Http.expectJson GotLogin (Decode.field "token" Decode.string)
        }


-- MAIN

main : Program () Model Msg
main =
    Browser.element
        { init = init
        , update = update
        , subscriptions = subscriptions
        , view = view
        }
