import React, { useState, useEffect } from 'react'
import { Dialog, Transition, TransitionChild, DialogPanel, DialogTitle } from '@headlessui/react'
import { X, AlertCircle, HelpCircle, CheckCircle, Activity } from 'lucide-react'
import { cn } from '../../utils/cn'

interface ModalProps {
  isOpen: boolean
  onClose: () => void
  title: string
  description?: string
  children?: React.ReactNode
  type?: 'info' | 'confirm' | 'error' | 'success'
  confirmLabel?: string
  cancelLabel?: string
  onConfirm?: () => void | Promise<void | boolean> | boolean
  maxWidth?: 'sm' | 'md' | 'lg' | 'xl' | '2xl'
}

export default function Modal({
  isOpen,
  onClose,
  title,
  description,
  children,
  type = 'info',
  confirmLabel = 'Confirm',
  cancelLabel = 'Cancel',
  onConfirm,
  maxWidth = 'md'
}: ModalProps) {
  const [isSubmitting, setIsSubmitting] = useState(false)
  
  useEffect(() => {
    if (isOpen) setIsSubmitting(false)
  }, [isOpen])

  const iconMap = {
    info: <HelpCircle className="w-6 h-6 text-blue-400" />,
    confirm: <HelpCircle className="w-6 h-6 text-amber-400" />,
    error: <AlertCircle className="w-6 h-6 text-red-400" />,
    success: <CheckCircle className="w-6 h-6 text-emerald-400" />
  }

  const maxWidthClasses = {
    sm: 'max-w-sm',
    md: 'max-w-md',
    lg: 'max-w-lg',
    xl: 'max-w-xl',
    '2xl': 'max-w-2xl'
  }

  return (
    <Transition show={isOpen} as={React.Fragment}>
      <Dialog as="div" className="relative z-[9999]" onClose={onClose}>
        <TransitionChild
          as={React.Fragment}
          enter="ease-out duration-300"
          enterFrom="opacity-0"
          enterTo="opacity-100"
          leave="ease-in duration-200"
          leaveFrom="opacity-100"
          leaveTo="opacity-0"
        >
          <div className="fixed inset-0 bg-black/80 backdrop-blur-sm transition-opacity" />
        </TransitionChild>

        <div className="fixed inset-0 z-10 w-screen overflow-y-auto">
          <div className="flex min-h-full items-end justify-center p-4 text-center sm:items-center sm:p-0">
            <TransitionChild
              as={React.Fragment}
              enter="ease-out duration-300"
              enterFrom="opacity-0 translate-y-4 sm:translate-y-0 sm:scale-95"
              enterTo="opacity-100 translate-y-0 sm:scale-100"
              leave="ease-in duration-200"
              leaveFrom="opacity-100 translate-y-0 sm:scale-100"
              leaveTo="opacity-0 translate-y-4 sm:translate-y-0 sm:scale-95"
            >
              <DialogPanel className={cn(
                "relative transform overflow-hidden rounded-2xl bg-neutral-900 border border-neutral-800 p-6 text-left shadow-2xl transition-all sm:my-8 w-full",
                maxWidthClasses[maxWidth]
              )}>
                <div className="flex items-start justify-between mb-4">
                  <div className="flex items-center gap-3">
                    <div className="p-2 rounded-xl bg-neutral-950 border border-neutral-800">
                      {iconMap[type]}
                    </div>
                    <DialogTitle as="h3" className="text-lg font-bold leading-6 text-white tracking-tight">
                      {title}
                    </DialogTitle>
                  </div>
                  <button
                    type="button"
                    className="rounded-lg p-1 text-neutral-500 hover:text-white hover:bg-neutral-800 transition-all focus-visible:ring-2 focus-visible:ring-offset-2 focus-visible:ring-neutral-500 ring-offset-neutral-900"
                    onClick={onClose}
                    aria-label="Close"
                  >
                    <X className="w-5 h-5" />
                  </button>
                </div>

                <div className="mt-2">
                  {description && (
                    <p className="text-sm text-neutral-400 font-medium leading-relaxed">
                      {description}
                    </p>
                  )}
                  {children && <div className="mt-4">{children}</div>}
                </div>

                <div className="mt-8 flex flex-col-reverse sm:flex-row sm:justify-end gap-3">
                  {(type === 'confirm' || type === 'info') && (
                    <button
                      type="button"
                      className="inline-flex w-full justify-center rounded-xl bg-neutral-800 px-4 py-2.5 text-sm font-bold text-white hover:bg-neutral-700 border border-neutral-700 shadow-sm transition-all sm:w-auto"
                      onClick={onClose}
                    >
                      {cancelLabel}
                    </button>
                  )}
                  {onConfirm && (
                    <button
                      type="button"
                      className={cn(
                        "inline-flex w-full justify-center rounded-xl px-4 py-2.5 text-sm font-bold text-white shadow-sm transition-all sm:w-auto",
                        type === 'error' ? "bg-red-600 hover:bg-red-500 border border-red-500/50" : "bg-blue-600 hover:bg-blue-500 border border-blue-500/50"
                      )}
                      onClick={async () => {
                        try {
                          setIsSubmitting(true)
                          const result = await onConfirm()
                          // Only close if the result isn't explicitly false
                          if (result !== false) {
                            onClose()
                          }
                        } catch (error) {
                          // Allow errors to bubble up or log them; here we log to prevent swallowing 
                          // but the caller is responsible for UI feedback before throwing if desired.
                          console.error("Modal confirmation failed:", error)
                        } finally {
                          setIsSubmitting(false)
                        }
                      }}
                      disabled={isSubmitting}
                    >
                      {isSubmitting ? (
                        <Activity className="w-4 h-4 mr-2 animate-spin" />
                      ) : null}
                      {confirmLabel}
                    </button>
                  )}
                </div>
              </DialogPanel>
            </TransitionChild>
          </div>
        </div>
      </Dialog>
    </Transition>
  )
}
