port module Main exposing (main)

import Browser
import Html exposing (Html, div, text, input, button, table, thead, tbody, tr, td, th)
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

-- MODEL

type alias User =
    { id : String
    , name : String
    , email : String
    }

type alias Model =
    { username : String
    , password : String
    , token : Maybe String
    , message : String
    , users : List User
    , page : Int
    , totalPages : Int
    }


-- INIT

init : () -> ( Model, Cmd Msg )
init _ =
    ( { username = "", password = "", token = Nothing, message = "", users = [], page = 1, totalPages = 1 }
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
    | GotProtectedResponse (Result Http.Error (List User, Int))
    | NextPage
    | PrevPage


-- DECODER

usersWithPagesDecoder : Decode.Decoder (List User, Int)
usersWithPagesDecoder =
    Decode.map2 Tuple.pair
        (Decode.field "users" (Decode.list userDecoder))
        (Decode.field "totalPages" Decode.int)


-- HTTP (UPDATED)

fetchProtectedResource : String -> Int -> Cmd Msg
fetchProtectedResource token page =
    let
        url = "http://127.0.0.1:5000/users?page=" ++ String.fromInt page
    in
    Http.request
        { method = "GET"
        , headers = [ Http.header "Authorization" ("Bearer " ++ token) ]
        , url = url
        , body = Http.emptyBody
        , expect = Http.expectJson GotProtectedResponse usersWithPagesDecoder
        , timeout = Nothing
        , tracker = Nothing
        }


-- VIEW (ADD PAGINATION CONTROLS)

viewPagination : Model -> Html Msg
viewPagination model =
    div [ style "margin-top" "1em" ]
        [ button [ onClick PrevPage, disabled (model.page <= 1) ] [ text "◀ Prev" ]
        , text (" Page " ++ String.fromInt model.page ++ " of " ++ String.fromInt model.totalPages ++ " ")
        , button [ onClick NextPage, disabled (model.page >= model.totalPages) ] [ text "Next ▶" ]
        ]


-- DECODERS

userDecoder : Decode.Decoder User
userDecoder =
    Decode.map3 User
        (Decode.field "id" Decode.string)
        (Decode.field "name" Decode.string)
        (Decode.field "email" Decode.string)

usersDecoder : Decode.Decoder (List User)
usersDecoder =
    Decode.list userDecoder


-- HTTP

loginRequest : String -> String -> Cmd Msg
loginRequest u p =
    Http.post
        { url = "http://127.0.0.1:5000/login"
        , body = Http.jsonBody <| Encode.object [ ("username", Encode.string u), ("password", Encode.string p) ]
        , expect = Http.expectJson GotLogin (Decode.field "token" Decode.string)
        }

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
            ( { model | token = Nothing, message = "Logged out", users = [] }, clearToken () )

        MakeProtectedRequest ->
            case model.token of
                Just token ->
                    ( model, fetchProtectedResource token model.page )

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

        GotProtectedResponse (Ok (users, totalPages)) ->
            ( { model
                | message = "Protected call success"
                , users = users
                , totalPages = totalPages
            }
            , Cmd.none
            )

        GotProtectedResponse (Err _) ->
            ( { model | message = "Protected call failed.", users = [] }, Cmd.none )

        NextPage ->
            let
                newPage = model.page + 1
            in
            case model.token of
                Just token -> ( { model | page = newPage }, fetchProtectedResource token newPage )
                Nothing -> ( model, Cmd.none )

        PrevPage ->
            let
                newPage = Basics.max 1 (model.page - 1)
            in
            case model.token of
                Just token -> ( { model | page = newPage }, fetchProtectedResource token newPage )
                Nothing -> ( model, Cmd.none )


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
        , viewUsers model.users
        , viewPagination model
        ]

viewUsers : List User -> Html msg
viewUsers users =
    if List.isEmpty users then
        text ""
    else
        table [ style "margin-top" "1em", style "border" "1px solid black", style "border-collapse" "collapse" ]
            [ thead []
                [ tr []
                    [ th [ style "border" "1px solid black", style "padding" "0.5em" ] [ text "ID" ]
                    , th [ style "border" "1px solid black", style "padding" "0.5em" ] [ text "Name" ]
                    , th [ style "border" "1px solid black", style "padding" "0.5em" ] [ text "Email" ]
                    ]
                ]
            , tbody [] (List.map viewUser users)
            ]

viewUser : User -> Html msg
viewUser user =
    tr []
        [ td [ style "border" "1px solid black", style "padding" "0.5em" ] [ text user.id ]
        , td [ style "border" "1px solid black", style "padding" "0.5em" ] [ text user.name ]
        , td [ style "border" "1px solid black", style "padding" "0.5em" ] [ text user.email ]
        ]


-- SUBSCRIPTIONS

subscriptions : Model -> Sub Msg
subscriptions _ =
    receiveToken ReceiveStoredToken


-- MAIN

main : Program () Model Msg
main =
    Browser.element
        { init = init
        , update = update
        , subscriptions = subscriptions
        , view = view
        }
