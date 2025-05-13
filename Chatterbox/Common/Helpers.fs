namespace Chatterbox.Common

module Helpers =
    let curry f = fun a b -> f (a, b)
    let uncurry f = fun (a, b) -> f a b
