module Main exposing (..)

import Browser
import Html exposing (Html, div, h1, table, thead, tbody, tr, td, th, text)
import Html.Attributes exposing (..)
import Http
import Json.Decode as Decode


-- MODEL

type alias User =
    { id : String
    , email : String
    , name : String
    }

type alias Model =
    { users : List User
    , error : Maybe String
    }

init : () -> ( Model, Cmd Msg )
init _ =
    ( { users = [], error = Nothing }
    , fetchUsers
    )


-- UPDATE

type Msg
    = GotUsers (Result Http.Error (List User))

update : Msg -> Model -> ( Model, Cmd Msg )
update msg model =
    case msg of
        GotUsers (Ok users) ->
            ( { model | users = users }, Cmd.none )

        GotUsers (Err e) ->
            ( { model | error = Just (Debug.toString e) }, Cmd.none )


-- VIEW

view : Model -> Html Msg
view model =
    div [ class "container" ]
        [ h1 [] [ text "ToS Agreement Tracker" ]
        , table []
            [ thead []
                [ tr []
                    [ th [] [ text "Name" ]
                    , th [] [ text "Email" ]
                    , th [] [ text "User ID" ]
                    ]
                ]
            , tbody []
                (List.map viewUser model.users)
            ]
        , case model.error of
            Just err -> div [ style "color" "red" ] [ text err ]
            Nothing -> text ""
        ]

viewUser : User -> Html msg
viewUser u =
    tr []
        [ td [] [ text u.name ]
        , td [] [ text u.email ]
        , td [] [ text u.id ]
        ]


-- HTTP

userDecoder : Decode.Decoder User
userDecoder =
    Decode.map3 User
        (Decode.field "id" Decode.string)
        (Decode.field "email" Decode.string)
        (Decode.field "name" Decode.string)

usersDecoder : Decode.Decoder (List User)
usersDecoder =
    Decode.list userDecoder

fetchUsers : Cmd Msg
fetchUsers =
    Http.get
        { url = "http://127.0.0.1:5000/users"
        , expect = Http.expectJson GotUsers usersDecoder
        }


-- MAIN

main : Program () Model Msg
main =
    Browser.element
        { init = init
        , update = update
        , subscriptions = always Sub.none
        , view = view
        }
