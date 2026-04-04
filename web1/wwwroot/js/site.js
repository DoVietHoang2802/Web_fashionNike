// AJAX Cart Management
// Global Add to Cart Function
function addToCart(productId, quantity = 1, selectedSize = null, selectedColor = null, event = null) {
    $.post('/Cart/AddToCart', {
        productId: productId,
        quantity: quantity,
        selectedSize: selectedSize,
        selectedColor: selectedColor
    }, function (response) {
        if (response.success) {
            // Update cart count in header
            const badge = $('#cart-count');
            badge.text(response.cartCount);
            if (response.cartCount > 0) badge.removeClass('d-none');
            else badge.addClass('d-none');
            
            // Trigger Fly to Cart Animation
            if (event) {
                animateFlyToCart(event);
            } else {
                showToast('Đã thêm sản phẩm vào giỏ hàng!');
            }
        } else {
            showToast(response.message || 'Có lỗi xảy ra', 'error');
        }
    }).fail(function() {
        showToast('Không thể kết nối đến máy chủ', 'error');
    });
}

async function removeFromCart(productId, size = null, color = null) {
    try {
        const response = await $.post('/Cart/RemoveFromCart', { productId, selectedSize: size, selectedColor: color });
        if (response.success) {
            // Update cart count in header
            const badge = $('#cart-count');
            badge.text(response.cartCount);
            if (response.cartCount > 0) badge.removeClass('d-none');
            else badge.addClass('d-none');
            showToast('Đã xóa sản phẩm khỏi giỏ hàng!');
        } else {
            showToast(response.message || 'Có lỗi xảy ra khi xóa sản phẩm.', 'error');
        }
    } catch (err) {
        console.error('Error removing from cart:', err);
        showToast('Không thể kết nối đến máy chủ', 'error');
    }
}

function animateFlyToCart(event) {
    const btn = $(event.currentTarget);
    const cart = $('.fa-shopping-cart').first(); // Target the cart icon in navbar
    
    if (!cart.length || !cart.offset()) return;

    // Find the product image or use the button as source
    let imgToFly = btn.closest('.product-card').find('img').first();
    if (!imgToFly.length) imgToFly = btn;
    if (!imgToFly.offset()) return;

    const imgClone = imgToFly.clone()
        .offset({
            top: imgToFly.offset().top,
            left: imgToFly.offset().left
        })
        .css({
            'opacity': '0.8',
            'position': 'absolute',
            'height': '150px',
            'width': '150px',
            'z-index': '9999',
            'border-radius': '50%',
            'object-fit': 'cover',
            'pointer-events': 'none'
        })
        .appendTo($('body'))
        .animate({
            'top': (cart.offset()?.top ?? 0) + 10,
            'left': (cart.offset()?.left ?? 0) + 10,
            'width': 20,
            'height': 20
        }, 1000, 'swing'); // Changed to swing for maximum compatibility

    setTimeout(function () {
        imgClone.remove();
        if (cart.parent().length) {
            cart.parent().addClass('animate__animated animate__rubberBand');
            setTimeout(() => cart.parent().removeClass('animate__animated animate__rubberBand'), 1000);
        }
    }, 1000);
}

function showToast(message, type = 'success') {
    // Remove existing toast container if it exists
    $('.toast-container').remove();
    
    const bgColor = type === 'success' ? 'rgba(16, 185, 129, 0.9)' : 'rgba(239, 68, 68, 0.9)';
    const toastHtml = `
        <div class="toast-container position-fixed bottom-0 end-0 p-3" style="z-index: 9999;">
            <div class="toast show border-0 rounded-4 shadow-lg overflow-hidden animate__animated animate__fadeInUp" style="background: ${bgColor}; color: white; backdrop-filter: blur(10px);">
                <div class="d-flex p-3 align-items-center">
                    <div class="me-2 text-white"><i class="fas ${type === 'success' ? 'fa-check-circle' : 'fa-exclamation-circle'}"></i></div>
                    <div class="fw-medium text-white">${message}</div>
                </div>
            </div>
        </div>
    `;
    const $toast = $(toastHtml).appendTo('body');
    setTimeout(() => {
        if ($toast.length) {
            $toast.find('.toast').removeClass('animate__fadeInUp').addClass('animate__fadeOutDown');
            setTimeout(() => $toast.remove(), 500);
        }
    }, 3000);
}



// Quick View Trigger
$(document).on('click', '.quick-view-trigger', function (e) {
    e.preventDefault();
    const productId = $(this).data('product-id');
    const $content = $('#quickViewContent');
    
    $content.html('<div class="text-center py-5"><div class="spinner-border text-primary"></div></div>');
    const modal = new bootstrap.Modal(document.getElementById('quickViewModal'));
    modal.show();
    
    $.get('/Products/QuickView/' + productId, function (html) {
        $content.html(html);
    }).fail(function() {
        $content.html('<div class="p-4 text-center text-danger">Không thể tải thông tin sản phẩm.</div>');
    });
});

$(document).ready(() => {
    // Other initializations can go here
});
